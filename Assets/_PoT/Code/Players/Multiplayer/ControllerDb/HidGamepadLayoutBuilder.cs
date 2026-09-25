using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.InputSystem;
using HID = UnityEngine.InputSystem.HID.HID;

/// <summary>
/// Couch — Option B, B-2. Turns a generic HID pad + its SDL_GameControllerDB mapping into a Unity layout
/// JSON that <c>extend</c>s "Gamepad", so the pad becomes a first-class Gamepad — every <c>&lt;Gamepad&gt;</c>
/// binding, the couch pairing, and the button-prompt HUDs then work with zero per-pad wiring.
///
/// <para><b>The correlation that makes this correct:</b> control offsets are ground truth from the device's
/// own HID descriptor, and HID report order == SDL's enumeration order. So the Nth Button element is SDL
/// <c>bN</c>, the Nth GenericDesktop axis element is <c>aN</c>, and the Hat element is <c>hN</c>. This is
/// exactly what made the hand-authored DragonRise layout correct (X was at byte 1, not byte 0) — generalized
/// so the same reasoning holds for any pad in the DB. Verified by <c>ControllerDbSelfTest</c>.</para>
///
/// <para>Grammar supported (per SDL_GameControllerDB): buttons <c>bN</c>; full axes <c>aN</c> / inverted
/// <c>aN~</c>; half axes <c>+aN</c>/<c>-aN</c> (dpad/triggers); hats <c>hN.M</c> (M = 1 up / 2 right / 4 down /
/// 8 left). Face buttons follow SDL's positional convention a=South, b=East, x=West, y=North.</para>
/// </summary>
public static class HidGamepadLayoutBuilder
{
    // ── One device's elements, in SDL index order ─────────────────────────────
    public struct BtnElem { public int byteOffset; public int bit; }
    public struct AxisElem { public int byteOffset; public int sizeInBits; public bool signed; public int logicalMin; public int logicalMax; public int usage; }
    public struct HatElem { public int byteOffset; public int bit; public int sizeInBits; }

    public class Tables
    {
        public readonly List<BtnElem> buttons = new List<BtnElem>();
        public readonly List<AxisElem> axes = new List<AxisElem>();
        public readonly List<HatElem> hats = new List<HatElem>();
    }

    /// <summary>Parse a device's HID descriptor into SDL-ordered button/axis/hat tables.</summary>
    public static bool TryReadTables(InputDevice device, out Tables tables)
    {
        tables = null;
        var caps = device != null ? device.description.capabilities : null;
        if (string.IsNullOrEmpty(caps)) return false;

        HID.HIDDeviceDescriptor hd;
        try { hd = HID.HIDDeviceDescriptor.FromJson(caps); }
        catch { return false; }
        if (hd.elements == null) return false;

        var btn = new List<(int off, BtnElem e)>();
        var axs = new List<(int off, AxisElem e)>();
        var hat = new List<(int off, HatElem e)>();

        foreach (var el in hd.elements)
        {
            if (el.reportType != HID.HIDReportType.Input) continue;
            if (el.isConstant) continue;

            if (el.usagePage == HID.UsagePage.Button)
            {
                int size = el.reportSizeInBits <= 0 ? 1 : el.reportSizeInBits;
                if (el.isArray && el.usageMin.HasValue && el.usageMax.HasValue && el.usageMax.Value >= el.usageMin.Value)
                {
                    int count = el.usageMax.Value - el.usageMin.Value + 1;   // array of consecutive 1-bit buttons
                    for (int k = 0; k < count; k++)
                    {
                        int ob = el.reportOffsetInBits + k * size;
                        btn.Add((ob, new BtnElem { byteOffset = ob / 8, bit = ob % 8 }));
                    }
                }
                else
                {
                    int ob = el.reportOffsetInBits;
                    btn.Add((ob, new BtnElem { byteOffset = ob / 8, bit = ob % 8 }));
                }
            }
            else if (el.usagePage == HID.UsagePage.GenericDesktop)
            {
                int usage = el.usage;
                if (usage == 0x39)   // Hat switch
                {
                    hat.Add((el.reportOffsetInBits, new HatElem
                    {
                        byteOffset = el.reportOffsetInBits / 8,
                        bit = el.reportOffsetInBits % 8,
                        sizeInBits = el.reportSizeInBits <= 0 ? 4 : el.reportSizeInBits
                    }));
                }
                else if (usage >= 0x30 && usage <= 0x38)   // X,Y,Z,Rx,Ry,Rz,Slider,Dial,Wheel
                {
                    axs.Add((el.reportOffsetInBits, new AxisElem
                    {
                        byteOffset = el.reportOffsetInBits / 8,
                        sizeInBits = el.reportSizeInBits <= 0 ? 8 : el.reportSizeInBits,
                        signed = el.logicalMin < 0,
                        logicalMin = el.logicalMin,
                        logicalMax = el.logicalMax,
                        usage = usage
                    }));
                }
            }
        }

        btn.Sort((a, b) => a.off.CompareTo(b.off));   // buttons: report order == SDL/DInput order
        axs.Sort((a, b) => a.off.CompareTo(b.off));
        hat.Sort((a, b) => a.off.CompareTo(b.off));

        tables = new Tables();
        foreach (var x in btn) tables.buttons.Add(x.e);

        // Axes: SDL/DirectInput enumerate by usage SLOT (X,Y,Z,Rx,Ry,Rz,Slider,Dial,Wheel), ONE per usage — not
        // by report order — and a duplicated usage (cheap pads sometimes declare Z twice) collapses to the first.
        // Mirror that so aN indices line up with the DB's mapping. (DragonRise: report X,Y,Z,Z,Rz → SDL X,Y,Z,Rz.)
        var seenUsage = new HashSet<int>();
        var dedupedAxes = new List<AxisElem>();
        foreach (var x in axs) if (seenUsage.Add(x.e.usage)) dedupedAxes.Add(x.e);   // first (lowest-offset) wins
        dedupedAxes.Sort((a, b) => a.usage.CompareTo(b.usage));                       // usage-value order == DInput slot order
        foreach (var a in dedupedAxes) tables.axes.Add(a);

        foreach (var x in hat) tables.hats.Add(x.e);
        return true;
    }

    /// <summary>Build a "Gamepad"-extending layout JSON for <paramref name="device"/> from its SDL mapping.
    /// False if the descriptor can't be read or nothing resolved.</summary>
    public static bool TryBuildJson(InputDevice device, Dictionary<string, string> map, string layoutName, out string json)
    {
        json = null;
        if (map == null || !TryReadTables(device, out var t)) return false;

        var c = new List<string>();

        // Face + shoulders + thumbs + start/back (button-sourced).
        AddButton(c, t, map, "a", "buttonSouth");
        AddButton(c, t, map, "b", "buttonEast");
        AddButton(c, t, map, "x", "buttonWest");
        AddButton(c, t, map, "y", "buttonNorth");
        AddButton(c, t, map, "leftshoulder", "leftShoulder");
        AddButton(c, t, map, "rightshoulder", "rightShoulder");
        AddButton(c, t, map, "back", "select");
        AddButton(c, t, map, "start", "start");
        AddButton(c, t, map, "leftstick", "leftStickPress");
        AddButton(c, t, map, "rightstick", "rightStickPress");

        // Sticks.
        AddStick(c, t, map, "leftx", "lefty", "leftStick");
        AddStick(c, t, map, "rightx", "righty", "rightStick");

        // Triggers (digital button OR analog axis).
        AddTrigger(c, t, map, "lefttrigger", "leftTrigger");
        AddTrigger(c, t, map, "righttrigger", "rightTrigger");

        // Dpad (hat, buttons, or half-axes).
        AddDpad(c, t, map);

        if (c.Count == 0) return false;

        var sb = new StringBuilder();
        sb.Append("{\n  \"name\" : \"").Append(layoutName).Append("\",\n");
        sb.Append("  \"extend\" : \"Gamepad\",\n");
        sb.Append("  \"format\" : \"HID \",\n");
        sb.Append("  \"controls\" : [\n");
        sb.Append(string.Join(",\n", c));
        sb.Append("\n  ]\n}");
        json = sb.ToString();
        return true;
    }

    // ── Emitters ──────────────────────────────────────────────────────────────
    private static void AddButton(List<string> outc, Tables t, Dictionary<string, string> map, string sdl, string control)
    {
        if (!map.TryGetValue(sdl, out var src)) return;
        if (!TryButtonIndex(src, out int i) || i < 0 || i >= t.buttons.Count) return;
        var b = t.buttons[i];
        outc.Add($"    {{ \"name\" : \"{control}\", \"layout\" : \"Button\", \"offset\" : {b.byteOffset}, \"bit\" : {b.bit}, \"format\" : \"BIT\", \"sizeInBits\" : 1 }}");
    }

    private static void AddStick(List<string> outc, Tables t, Dictionary<string, string> map, string sdlX, string sdlY, string stick)
    {
        AxisElem ax = default, ay = default;
        bool okX = false, okY = false, invX = false, invY = false;
        if (map.TryGetValue(sdlX, out var sx) && ParseAxis(sx, out int ix, out bool tX, out _) && ix < t.axes.Count)
        { ax = t.axes[ix]; invX = tX; okX = true; }              // X: Unity right = +, invert only on SDL '~'
        if (map.TryGetValue(sdlY, out var sy) && ParseAxis(sy, out int iy, out bool tY, out _) && iy < t.axes.Count)
        { ay = t.axes[iy]; invY = !tY; okY = true; }             // Y: HID down = high value → invert by default
        if (!okX && !okY) return;

        // Anchor the parent Stick at an explicit byte; child offsets are RELATIVE to it. A composite parent with
        // NO offset while its children DO have offsets makes Unity's offset resolver recurse → stack overflow.
        int baseByte = okX ? ax.byteOffset : ay.byteOffset;
        bool eightBit = (!okX || ax.sizeInBits == 8) && (!okY || ay.sizeInBits == 8);
        if (eightBit)
            outc.Add($"    {{ \"name\" : \"{stick}\", \"layout\" : \"Stick\", \"offset\" : {baseByte}, \"format\" : \"VC2B\" }}");
        else
            outc.Add($"    {{ \"name\" : \"{stick}\", \"layout\" : \"Stick\", \"offset\" : {baseByte} }}");
        if (okX) outc.Add(AxisJson($"{stick}/x", ax.byteOffset - baseByte, ax, invX));
        if (okY) outc.Add(AxisJson($"{stick}/y", ay.byteOffset - baseByte, ay, invY));
    }

    private static void AddTrigger(List<string> outc, Tables t, Dictionary<string, string> map, string sdl, string control)
    {
        if (!map.TryGetValue(sdl, out var src)) return;
        if (TryButtonIndex(src, out int bi) && bi >= 0 && bi < t.buttons.Count)   // digital trigger
        {
            var b = t.buttons[bi];
            outc.Add($"    {{ \"name\" : \"{control}\", \"offset\" : {b.byteOffset}, \"bit\" : {b.bit}, \"format\" : \"BIT\" }}");
            return;
        }
        if (ParseAxis(src, out int ai, out _, out _) && ai < t.axes.Count)         // analog trigger
        {
            var a = t.axes[ai];
            if (a.sizeInBits == 8)
                outc.Add($"    {{ \"name\" : \"{control}\", \"offset\" : {a.byteOffset}, \"format\" : \"BYTE\" }}");
            else
                outc.Add($"    {{ \"name\" : \"{control}\", \"offset\" : {a.byteOffset}, \"format\" : \"SHRT\" }}");
        }
    }

    private static void AddDpad(List<string> outc, Tables t, Dictionary<string, string> map)
    {
        // Hat form (dpup:h0.1 …) — all four directions share one 4-bit hat element (0=up,2=right,4=down,6=left).
        if (map.TryGetValue("dpup", out var up) && up.Length > 0 && up[0] == 'h' &&
            TryHatIndex(up, out int hi) && hi < t.hats.Count)
        {
            var h = t.hats[hi];
            outc.Add($"    {{ \"name\" : \"dpad\", \"offset\" : {h.byteOffset}, \"bit\" : {h.bit}, \"format\" : \"BIT\", \"layout\" : \"Dpad\", \"sizeInBits\" : 4, \"defaultState\" : 8 }}");
            // Children read the SAME hat field as the parent → offset 0 (relative to the parent), same bit.
            outc.Add($"    {{ \"name\" : \"dpad/up\", \"offset\" : 0, \"bit\" : {h.bit}, \"sizeInBits\" : 4, \"format\" : \"BIT\", \"layout\" : \"DiscreteButton\", \"parameters\" : \"minValue=7,maxValue=1,nullValue=8,wrapAtValue=7\" }}");
            outc.Add($"    {{ \"name\" : \"dpad/right\", \"offset\" : 0, \"bit\" : {h.bit}, \"sizeInBits\" : 4, \"format\" : \"BIT\", \"layout\" : \"DiscreteButton\", \"parameters\" : \"minValue=1,maxValue=3\" }}");
            outc.Add($"    {{ \"name\" : \"dpad/down\", \"offset\" : 0, \"bit\" : {h.bit}, \"sizeInBits\" : 4, \"format\" : \"BIT\", \"layout\" : \"DiscreteButton\", \"parameters\" : \"minValue=3,maxValue=5\" }}");
            outc.Add($"    {{ \"name\" : \"dpad/left\", \"offset\" : 0, \"bit\" : {h.bit}, \"sizeInBits\" : 4, \"format\" : \"BIT\", \"layout\" : \"DiscreteButton\", \"parameters\" : \"minValue=5,maxValue=7\" }}");
            return;
        }

        // Button or half-axis form. Anchor a Dpad parent at 0 so each direction's ABSOLUTE offset lands correctly
        // (child offset is relative to the parent; an explicit parent offset also avoids the resolver recursion).
        if (map.ContainsKey("dpup") || map.ContainsKey("dpdown") || map.ContainsKey("dpleft") || map.ContainsKey("dpright"))
            outc.Add("    { \"name\" : \"dpad\", \"offset\" : 0, \"layout\" : \"Dpad\" }");
        AddDpadDir(outc, t, map, "dpup", "dpad/up", low: true);
        AddDpadDir(outc, t, map, "dpdown", "dpad/down", low: false);
        AddDpadDir(outc, t, map, "dpleft", "dpad/left", low: true);
        AddDpadDir(outc, t, map, "dpright", "dpad/right", low: false);
    }

    // One dpad direction from a button (dpup:b11) or a half-axis (dpup:-a1). 'low' = pressed at the axis's low end.
    private static void AddDpadDir(List<string> outc, Tables t, Dictionary<string, string> map, string sdl, string control, bool low)
    {
        if (!map.TryGetValue(sdl, out var src)) return;
        if (TryButtonIndex(src, out int bi) && bi >= 0 && bi < t.buttons.Count)
        {
            var b = t.buttons[bi];
            outc.Add($"    {{ \"name\" : \"{control}\", \"layout\" : \"Button\", \"offset\" : {b.byteOffset}, \"bit\" : {b.bit}, \"format\" : \"BIT\", \"sizeInBits\" : 1 }}");
            return;
        }
        if (ParseAxis(src, out int ai, out _, out char half) && ai < t.axes.Count)
        {
            var a = t.axes[ai];
            if (half == '-') low = true; else if (half == '+') low = false;
            int range = a.logicalMax - a.logicalMin;
            int margin = range > 0 ? range / 4 : (a.sizeInBits == 8 ? 64 : 8192);
            int lo = low ? a.logicalMin : a.logicalMax - margin;
            int hi = low ? a.logicalMin + margin : a.logicalMax;
            string fmt = a.sizeInBits == 8 ? "BYTE" : "SHRT";
            outc.Add($"    {{ \"name\" : \"{control}\", \"offset\" : {a.byteOffset}, \"format\" : \"{fmt}\", \"layout\" : \"DiscreteButton\", \"parameters\" : \"minValue={lo},maxValue={hi}\" }}");
        }
    }

    // ── Formatting / parsing helpers ──────────────────────────────────────────
    // offset is emitted verbatim — callers pass an ABSOLUTE offset for top-level controls, or an offset RELATIVE
    // to the parent for a stick child.
    private static string AxisJson(string name, int offset, AxisElem a, bool invert)
    {
        if (a.sizeInBits == 8)
        {
            // Unsigned 8-bit centred at 0x80 → [-1,1].
            string p = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5" + (invert ? ",invert" : "");
            return $"    {{ \"name\" : \"{name}\", \"offset\" : {offset}, \"format\" : \"BYTE\", \"parameters\" : \"{p}\" }}";
        }
        // 16-bit signed short auto-normalizes to [-1,1]; just flip if needed.
        return invert
            ? $"    {{ \"name\" : \"{name}\", \"offset\" : {offset}, \"format\" : \"SHRT\", \"parameters\" : \"invert\" }}"
            : $"    {{ \"name\" : \"{name}\", \"offset\" : {offset}, \"format\" : \"SHRT\" }}";
    }

    private static bool TryButtonIndex(string src, out int idx)
    {
        idx = -1;
        if (string.IsNullOrEmpty(src) || src[0] != 'b') return false;
        return int.TryParse(src.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
    }

    // Parse an axis source: aN | aN~ | +aN | -aN | +aN~ …  → index, invert(~), half('+','-',' ').
    private static bool ParseAxis(string src, out int idx, out bool invert, out char half)
    {
        idx = -1; invert = false; half = ' ';
        if (string.IsNullOrEmpty(src)) return false;
        int i = 0;
        if (src[0] == '+' || src[0] == '-') { half = src[0]; i = 1; }
        if (i >= src.Length || src[i] != 'a') return false;
        i++;
        int start = i;
        while (i < src.Length && char.IsDigit(src[i])) i++;
        if (i == start) return false;
        if (!int.TryParse(src.Substring(start, i - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out idx)) return false;
        if (i < src.Length && src[i] == '~') invert = true;
        return true;
    }

    private static bool TryHatIndex(string src, out int idx)
    {
        idx = -1;
        if (string.IsNullOrEmpty(src) || src[0] != 'h') return false;
        int dot = src.IndexOf('.');
        string num = dot > 1 ? src.Substring(1, dot - 1) : src.Substring(1);
        return int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
    }
}
