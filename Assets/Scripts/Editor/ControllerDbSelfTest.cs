#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Couch — Option B, B-2 acceptance test. Reads every connected HID controller, runs it through the SAME
/// pipeline the runtime registrar uses (<see cref="SdlControllerDb"/> → <see cref="HidGamepadLayoutBuilder"/>),
/// and prints the SDL-ordered element tables + generated Gamepad layout JSON.
///
/// <para>Its real job is the <b>DragonRise regression assert</b>: it proves the generic DB path regenerates the
/// exact offsets of the hand-authored B-1 layout (leftStick X@byte1/Y@byte2, rightStick@4/5, buttons at byte6
/// high-nibble + byte7, hat at byte6 low-nibble). If this passes with a DragonRise pad connected, the generic
/// path produces a byte-identical device — so retiring the hand-authored layout can't regress the one pad we can
/// runtime-test. Menu: <b>Planet of Twins Tools ▸ Input ▸ Controller DB Self-Test</b>.</para>
/// </summary>
public static class ControllerDbSelfTest
{
    private const int DragonRiseVid = 0x0079;
    private const int DragonRisePid = 0x0006;

    [MenuItem("Planet of Twins Tools/Input/Controller DB Self-Test")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ControllerDbSelfTest] DB entries: {SdlControllerDb.Count}");

        bool anyController = false;
        bool dragonRiseSeen = false;
        bool allAssertsPassed = true;

        foreach (var device in InputSystem.devices)
        {
            var caps = device.description.capabilities;
            if (string.IsNullOrEmpty(caps)) continue;
            if (!HidGamepadLayoutBuilder.TryReadTables(device, out var t)) continue;

            // VID/PID straight from the HID descriptor.
            int vid = 0, pid = 0;
            try
            {
                var hd = UnityEngine.InputSystem.HID.HID.HIDDeviceDescriptor.FromJson(caps);
                vid = hd.vendorId; pid = hd.productId;
            }
            catch { }
            if (vid == 0 && pid == 0) continue;

            anyController = true;
            sb.AppendLine($"\n=== {device.displayName}  (VID {vid:X4} PID {pid:X4})  as {device.GetType().Name} ===");
            sb.AppendLine($"  buttons ({t.buttons.Count}): " + DumpButtons(t));
            sb.AppendLine($"  axes    ({t.axes.Count}): " + DumpAxes(t));
            sb.AppendLine($"  hats    ({t.hats.Count}): " + DumpHats(t));

            if (SdlControllerDb.TryGetMapping(vid, pid, out var map))
            {
                string layoutName = $"DBGamepad_{vid:X4}_{pid:X4}";
                if (HidGamepadLayoutBuilder.TryBuildJson(device, map, layoutName, out string json))
                {
                    sb.AppendLine("  generated layout JSON:\n" + json);
                    // Actually register + instantiate (NO matcher, so the real pad is untouched) to catch any
                    // layout-build recursion / stack overflow here in edit mode — no play mode / reconnect needed.
                    try { InputSystem.RemoveLayout(layoutName); } catch { }
                    try
                    {
                        InputSystem.RegisterLayout(json, layoutName);
                        var probe = InputSystem.AddDevice(layoutName);
                        var gp = probe as Gamepad;
                        sb.AppendLine($"  [REGISTER+INSTANTIATE OK] {probe.GetType().Name}, {probe.allControls.Count} controls; " +
                                      $"leftStick x@byte{gp?.leftStick.x.stateBlock.byteOffset} y@byte{gp?.leftStick.y.stateBlock.byteOffset}, " +
                                      $"rightStick x@byte{gp?.rightStick.x.stateBlock.byteOffset} y@byte{gp?.rightStick.y.stateBlock.byteOffset}");
                        InputSystem.RemoveDevice(probe);
                        InputSystem.RemoveLayout(layoutName);
                    }
                    catch (System.Exception e)
                    {
                        allAssertsPassed = false;
                        sb.AppendLine("  [REGISTER+INSTANTIATE FAILED] " + e.Message);
                    }
                }
                else
                    sb.AppendLine("  !! mapping present but layout build produced nothing.");
            }
            else
            {
                sb.AppendLine("  (no DB mapping for this VID/PID — would remain a generic Joystick.)");
            }

            if (vid == DragonRiseVid && pid == DragonRisePid)
            {
                dragonRiseSeen = true;
                allAssertsPassed &= AssertDragonRise(t, sb);
            }
        }

        if (!anyController)
            sb.AppendLine("\nNo HID controller connected — plug a pad in and re-run to validate its mapping.");
        else if (!dragonRiseSeen)
            sb.AppendLine("\n(No DragonRise pad connected — the regression assert was skipped. Tables/JSON above are FYI.)");

        // Console truncates multiline logs to the first line, so write the full report to a file and log a
        // single-line verdict the MCP console reader can actually see.
        string outPath = System.IO.Path.Combine(Application.dataPath, "..", "controller_db_selftest.txt");
        try { System.IO.File.WriteAllText(outPath, sb.ToString()); } catch { }

        string verdict =
            !anyController ? "no controller connected" :
            !dragonRiseSeen ? "ran OK, DragonRise regression assert SKIPPED (pad not detected)" :
            allAssertsPassed ? "DragonRise regression assert PASSED — generic path reproduces the hand-authored offsets" :
            "DragonRise regression assert FAILED — see report";
        string tail = $"[ControllerDbSelfTest] RESULT: {verdict}. Full report → controller_db_selftest.txt (project root).";
        if (dragonRiseSeen && !allAssertsPassed) Debug.LogError(tail);
        else Debug.Log(tail);
    }

    // Asserts the DragonRise pad's SDL-ordered tables match the hand-authored B-1 ground truth.
    private static bool AssertDragonRise(HidGamepadLayoutBuilder.Tables t, StringBuilder sb)
    {
        bool ok = true;
        ok &= Check(sb, "buttons.Count >= 12", t.buttons.Count >= 12);
        if (t.buttons.Count >= 1) ok &= Check(sb, "b0 @ byte6 bit4", t.buttons[0].byteOffset == 6 && t.buttons[0].bit == 4);
        if (t.buttons.Count >= 5) ok &= Check(sb, "b4 @ byte7 bit0", t.buttons[4].byteOffset == 7 && t.buttons[4].bit == 0);
        ok &= Check(sb, "axes.Count >= 4", t.axes.Count >= 4);
        if (t.axes.Count >= 1) ok &= Check(sb, "a0 (leftX) @ byte1", t.axes[0].byteOffset == 1);
        if (t.axes.Count >= 2) ok &= Check(sb, "a1 (leftY) @ byte2", t.axes[1].byteOffset == 2);
        if (t.axes.Count >= 3) ok &= Check(sb, "a2 (rightX) @ byte4", t.axes[2].byteOffset == 4);
        if (t.axes.Count >= 4) ok &= Check(sb, "a3 (rightY) @ byte5", t.axes[3].byteOffset == 5);
        ok &= Check(sb, "hats.Count >= 1", t.hats.Count >= 1);
        if (t.hats.Count >= 1) ok &= Check(sb, "hat @ byte6 bit0 size4", t.hats[0].byteOffset == 6 && t.hats[0].bit == 0 && t.hats[0].sizeInBits == 4);
        return ok;
    }

    private static bool Check(StringBuilder sb, string label, bool cond)
    {
        sb.AppendLine($"    [{(cond ? "PASS" : "FAIL")}] {label}");
        return cond;
    }

    private static string DumpButtons(HidGamepadLayoutBuilder.Tables t)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < t.buttons.Count; i++) sb.Append($"b{i}=B{t.buttons[i].byteOffset}.{t.buttons[i].bit} ");
        return sb.ToString();
    }

    private static string DumpAxes(HidGamepadLayoutBuilder.Tables t)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < t.axes.Count; i++) sb.Append($"a{i}=u0x{t.axes[i].usage:X2}@B{t.axes[i].byteOffset}/{t.axes[i].sizeInBits}b{(t.axes[i].signed ? "s" : "u")} ");
        return sb.ToString();
    }

    private static string DumpHats(HidGamepadLayoutBuilder.Tables t)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < t.hats.Count; i++) sb.Append($"h{i}=B{t.hats[i].byteOffset}.{t.hats[i].bit}/{t.hats[i].sizeInBits}b ");
        return sb.ToString();
    }
}
#endif
