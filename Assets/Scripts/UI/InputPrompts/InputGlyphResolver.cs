using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P1.4 (button glyphs) — the one place "an action → the right glyph for who's looking" is resolved. Combines:
///   • P1.1  the actual bound CONTROL PATH  (<see cref="IInputProvider.TryGetBindingControlPath"/>, rebinding-safe)
///   • device KIND  — the occupant's fixed <see cref="IInputProvider.PairedDeviceKind"/> in couch, else the live
///     <see cref="LastUsedDeviceTracker"/> (shared cursor / solo)
///   • the <see cref="InputGlyphMap"/> (kind, path) → Sprite lookup
///
/// Static service (loads the map from Resources once; the map is tiny, sprites load on demand). Returns null when
/// there is no glyph — the caller falls back to the binding TEXT (also handed back), never a blank.
/// </summary>
public static class InputGlyphResolver
{
    private const string MapResourcePath = "InputGlyphMap";   // Assets/Resources/InputGlyphMap.asset

    private static InputGlyphMap _map;
    private static Dictionary<(InputDeviceKind, string), InputGlyphMap.Entry> _lookup;
    private static bool _loadAttempted;

    private static void EnsureLoaded()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;

        _map = Resources.Load<InputGlyphMap>(MapResourcePath);
        _lookup = new Dictionary<(InputDeviceKind, string), InputGlyphMap.Entry>();
        if (_map == null)
        {
            Debug.LogWarning($"[InputGlyphResolver] No '{MapResourcePath}' in a Resources folder — prompts fall " +
                             "back to text. Run Planet of Twins Tools ▸ Input ▸ Bake Glyph Map.");
            return;
        }
        foreach (var e in _map.Entries)
            if (!string.IsNullOrEmpty(e.controlPath))
                _lookup[(e.kind, e.controlPath)] = e;
    }

    /// <summary>Which device family a prompt from <paramref name="provider"/> should represent: the occupant's
    /// paired device in couch, else the live last-used family (shared cursor / solo).</summary>
    public static InputDeviceKind ResolveKind(IInputProvider provider)
        => provider?.PairedDeviceKind ?? LastUsedDeviceTracker.LastUsed;

    /// <summary>Resolve the glyph Sprite for <paramref name="actionName"/> as <paramref name="provider"/> sees it.
    /// Always sets <paramref name="fallbackText"/> to the binding text so a missing glyph still shows something.
    /// Returns null when there is no glyph (or the map isn't baked) — draw <paramref name="fallbackText"/> then.</summary>
    public static Sprite ResolveSprite(IInputProvider provider, string actionName, out string fallbackText)
    {
        fallbackText = null;
        if (provider == null || string.IsNullOrEmpty(actionName)) return null;

        var kind = ResolveKind(provider);
        fallbackText = provider.GetBindingDisplay(actionName, kind == InputDeviceKind.Gamepad);

        if (!provider.TryGetBindingControlPath(actionName, kind, out var path, out _)) return null;

        EnsureLoaded();
        return _lookup.TryGetValue((kind, path), out var e) ? e.sprite : null;
    }

    /// <summary>Inline variant (P2 / decision 4): the TMP sprite name for <c>&lt;sprite name="…"&gt;</c>, or null.
    /// <paramref name="fallbackText"/> is always the binding text for the no-glyph path.</summary>
    public static string ResolveTmpSpriteName(IInputProvider provider, string actionName, out string fallbackText)
    {
        fallbackText = null;
        if (provider == null || string.IsNullOrEmpty(actionName)) return null;

        var kind = ResolveKind(provider);
        fallbackText = provider.GetBindingDisplay(actionName, kind == InputDeviceKind.Gamepad);

        if (!provider.TryGetBindingControlPath(actionName, kind, out var path, out _)) return null;

        EnsureLoaded();
        return _lookup.TryGetValue((kind, path), out var e) && !string.IsNullOrEmpty(e.tmpSpriteName)
            ? e.tmpSpriteName : null;
    }

    /// <summary>Inline TMP sprite name for an EXPLICIT device kind (shared multi-device prompts that show every
    /// active family — QTE combined-mash on one screen — not just the resolved one). <paramref name="provider"/>
    /// is only read for the binding PATH (bindings are global on the action asset, so any live provider works);
    /// the KIND is forced. Null when no glyph for that kind; <paramref name="fallbackText"/> is always set.</summary>
    public static string ResolveTmpSpriteNameForKind(IInputProvider provider, string actionName,
                                                     InputDeviceKind kind, out string fallbackText)
    {
        fallbackText = null;
        if (provider == null || string.IsNullOrEmpty(actionName)) return null;

        fallbackText = provider.GetBindingDisplay(actionName, kind == InputDeviceKind.Gamepad);

        if (!provider.TryGetBindingControlPath(actionName, kind, out var path, out _)) return null;

        EnsureLoaded();
        return _lookup.TryGetValue((kind, path), out var e) && !string.IsNullOrEmpty(e.tmpSpriteName)
            ? e.tmpSpriteName : null;
    }
}
