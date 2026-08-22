using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Couch — Option B, B-2 ("any controller"). Loads and indexes the community SDL_GameControllerDB
/// (<c>Assets/Settings/Input/Resources/gamecontrollerdb.txt</c>) by USB vendor/product id so an
/// unrecognized generic HID pad can be promoted to a real <see cref="UnityEngine.InputSystem.Gamepad"/>
/// from its published button/axis mapping. See <see cref="HidGamepadLayoutBuilder"/> (turns a mapping +
/// the pad's HID descriptor into a Unity layout) and <see cref="UniversalGamepadRegistrar"/> (wires it up
/// at runtime). This is the general form of the hand-authored DragonRise proof (B-1).
///
/// <para>Data-only + lazily cached (R7 spirit): the DB is parsed once on first lookup and never mutated.
/// Keyed by (vendorId, productId) — SDL's per-platform axis/button indices are relative to that platform's
/// enumeration, so we prefer the row whose <c>platform:</c> matches the build target.</para>
/// </summary>
public static class SdlControllerDb
{
    // key = (vid << 16) | pid  →  SDL target ("a","leftx","dpup",…) → source token ("b2","a0","+a1","h0.1","a1~").
    private static Dictionary<int, Dictionary<string, string>> _byVidPid;

    // Resource path (no extension), relative to a Resources/ folder.
    private const string ResourcePath = "gamecontrollerdb";

    public static int Count { get { EnsureLoaded(); return _byVidPid.Count; } }

    /// <summary>SDL mapping (target→source) for a USB HID pad, or false if the DB has no entry for it.</summary>
    public static bool TryGetMapping(int vendorId, int productId, out Dictionary<string, string> mapping)
    {
        EnsureLoaded();
        return _byVidPid.TryGetValue((vendorId << 16) | (productId & 0xFFFF), out mapping);
    }

    private static void EnsureLoaded()
    {
        if (_byVidPid != null) return;
        _byVidPid = new Dictionary<int, Dictionary<string, string>>();

        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError("[SdlControllerDb] 'gamecontrollerdb' not found under a Resources/ folder — generic " +
                           "controller support (Option B) is limited to natively-recognized pads. Expected " +
                           "Assets/Settings/Input/Resources/gamecontrollerdb.txt.");
            return;
        }

        var chosenPriority = new Dictionary<int, int>();   // per key: platform priority of the stored row
        foreach (var rawLine in asset.text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var tokens = line.Split(',');
            if (tokens.Length < 3) continue;               // guid, name, at least one mapping
            string guid = tokens[0];
            if (!TryVidPid(guid, out int vid, out int pid) || vid == 0 || pid == 0) continue;

            var map = new Dictionary<string, string>();
            string platform = "";
            for (int i = 2; i < tokens.Length; i++)        // 0 = guid, 1 = name
            {
                var tok = tokens[i];
                int c = tok.IndexOf(':');
                if (c <= 0 || c >= tok.Length - 1) continue;
                string target = tok.Substring(0, c);
                string source = tok.Substring(c + 1);
                if (target == "platform") { platform = source; continue; }
                if (target == "crc") continue;
                map[target] = source;
            }
            if (map.Count == 0) continue;

            int key = (vid << 16) | (pid & 0xFFFF);
            int prio = PlatformPriority(platform);
            if (_byVidPid.ContainsKey(key) && chosenPriority.TryGetValue(key, out int have) && have >= prio)
                continue;                                   // keep the higher-priority (build-target) row
            _byVidPid[key] = map;
            chosenPriority[key] = prio;
        }

        Debug.Log($"[SdlControllerDb] Loaded {_byVidPid.Count} controller mappings (by VID/PID).");
    }

    // SDL indices are platform-specific — prefer the row for the platform we actually run on.
    private static int PlatformPriority(string platform)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return platform == "Windows" ? 3 : platform.Length == 0 ? 1 : 0;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        return platform == "Mac OS X" ? 3 : platform.Length == 0 ? 1 : 0;
#else
        return platform == "Linux" ? 3 : platform.Length == 0 ? 1 : 0;
#endif
    }

    // SDL 32-hex GUID: USB vendor is little-endian at bytes 4-5 (chars 8-11), product at bytes 8-9 (chars 16-19).
    private static bool TryVidPid(string guid, out int vid, out int pid)
    {
        vid = pid = 0;
        if (string.IsNullOrEmpty(guid) || guid.Length < 20) return false;
        if (!TryByte(guid, 8, out int v0) || !TryByte(guid, 10, out int v1)) return false;
        if (!TryByte(guid, 16, out int p0) || !TryByte(guid, 18, out int p1)) return false;
        vid = v0 | (v1 << 8);
        pid = p0 | (p1 << 8);
        return true;
    }

    private static bool TryByte(string s, int at, out int val) =>
        int.TryParse(s.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val);
}
