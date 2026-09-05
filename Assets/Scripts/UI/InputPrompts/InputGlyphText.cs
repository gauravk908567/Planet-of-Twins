using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// P2b (button glyphs, inline) — the one helper that turns an input action into TMP rich-text for a given viewer.
/// It emits <c>&lt;sprite name="stem"&gt;</c> when a device-aware glyph resolves (via
/// <see cref="InputGlyphResolver.ResolveTmpSpriteName"/>), else a bracketed key/button label so a missing glyph
/// still reads as a control — never a blank.
///
/// <para><b>Why name-only, not <c>&lt;sprite="InputGlyphs" name=…&gt;</c>:</b> TMP's parser rejects the combined
/// asset+name form (it renders the tag literally). The name-only form resolves against the label's OWN sprite
/// asset — so <see cref="Apply"/> assigns our packed <c>InputGlyphs</c> asset to the label before setting text.
/// That keeps it self-contained: no TMP-Settings default and no per-scene wiring.</para>
///
/// <para>Every text surface (rescue F/E, QTE prompt, tutorial dismiss, ability-HUD key-caps, world legends) calls
/// <see cref="Apply"/> with the RIGHT provider — the occupant's in couch (<c>PlayerInputRouter.For(...)</c>), the
/// shared/last-used one otherwise — and re-applies on <see cref="LastUsedDeviceTracker.OnLastUsedChanged"/> so the
/// prompt follows the device the player just touched (Overwatch-style). Inline sibling of the Image-based
/// <see cref="InputPromptView"/>; both sit on <see cref="InputGlyphResolver"/>.</para>
/// </summary>
public static class InputGlyphText
{
    /// <summary>Resources path of the packed TMP sprite asset baked by <c>Bake TMP Glyph Sprite Asset</c>
    /// (Assets/Resources/Sprites/InputGlyphs.asset).</summary>
    private const string SpriteAssetResourcePath = "Sprites/InputGlyphs";

    private static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

    private static TMP_SpriteAsset _spriteAsset;
    private static bool _loadAttempted;

    /// <summary>The packed glyph sprite asset, loaded from Resources once. Null (with one warning) if not baked.</summary>
    public static TMP_SpriteAsset SpriteAsset
    {
        get
        {
            if (!_loadAttempted)
            {
                _loadAttempted = true;
                _spriteAsset = Resources.Load<TMP_SpriteAsset>(SpriteAssetResourcePath);
                if (_spriteAsset == null)
                    Debug.LogWarning($"[InputGlyphText] No sprite asset at Resources/{SpriteAssetResourcePath} — " +
                                     "inline glyphs fall back to text. Run Planet of Twins Tools ▸ Input ▸ " +
                                     "Bake TMP Glyph Sprite Asset.");
            }
            return _spriteAsset;
        }
    }

    /// <summary>Inline markup for one action as <paramref name="provider"/> sees it: <c>&lt;sprite name="stem"&gt;</c>
    /// if a glyph resolves, else a bracketed key/button label, else the action name. Never null/empty for a real action.</summary>
    public static string Glyph(IInputProvider provider, string actionName)
    {
        if (string.IsNullOrEmpty(actionName)) return actionName;

        string stem = InputGlyphResolver.ResolveTmpSpriteName(provider, actionName, out string fallback);
        if (!string.IsNullOrEmpty(stem))
            return $"<sprite name=\"{stem}\">";

        if (!string.IsNullOrEmpty(fallback)) return $"[{fallback}]";
        return actionName;
    }

    /// <summary>Substitute every <c>{ActionName}</c> token in <paramref name="template"/> with that action's glyph
    /// (or fallback) for <paramref name="provider"/>. Non-token text is left untouched. e.g.
    /// <c>Format("Press {Interact} to bond", p)</c> → <c>"Press &lt;sprite name=…&gt; to bond"</c>.</summary>
    public static string Format(string template, IInputProvider provider)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return TokenPattern.Replace(template, m => Glyph(provider, m.Groups[1].Value));
    }

    /// <summary>The consumer entry point: ensure <paramref name="label"/> can draw our glyphs (assigns the sprite
    /// asset once), then set its text to <paramref name="template"/> with every <c>{Action}</c> token substituted.
    /// Call this whenever the label's text OR the active device changes.</summary>
    public static void Apply(TMP_Text label, string template, IInputProvider provider)
    {
        if (label == null) return;
        var asset = SpriteAsset;
        if (asset != null && label.spriteAsset != asset) label.spriteAsset = asset;
        label.text = Format(template, provider);
    }

    /// <summary>Joint/shared-ability key-cap (couch): show ONE glyph when both players are on the same device
    /// KIND (both pads → one pad icon; both keyboard / solo P2→P1 → one), or BOTH glyphs side by side when the
    /// kinds differ (1 keyboard + 1 pad → the keyboard key then the pad button — "half and half"). Left glyph is
    /// <paramref name="providerA"/>'s (e.g. Lyra), right is <paramref name="providerB"/>'s (e.g. Kai). Assigns the
    /// packed sprite asset like <see cref="Apply"/>. The kind (not the stem) is compared so two identical pads
    /// never split — matching the stated rule exactly.</summary>
    public static void ApplyJoint(TMP_Text label, IInputProvider providerA, IInputProvider providerB, string actionName)
    {
        if (label == null) return;
        var asset = SpriteAsset;
        if (asset != null && label.spriteAsset != asset) label.spriteAsset = asset;

        bool sameKind = providerB == null ||
                        InputGlyphResolver.ResolveKind(providerA) == InputGlyphResolver.ResolveKind(providerB);
        label.text = sameKind
            ? Glyph(providerA, actionName)                              // one icon (both share a device kind)
            : Glyph(providerA, actionName) + Glyph(providerB, actionName); // half-half (keyboard + pad)
    }

    // ── Shared multi-device prompt (couch, one screen) ────────────────────────────────────────────────────────
    private const string SharedSeparator = "  |  ";

    /// <summary>Inline markup for one action across EVERY active device family, joined by " | " — for a SHARED
    /// prompt that either/both players can act on, on one screen (QTE combined-mash). Keyboard-only → one glyph;
    /// keyboard + pad → "F | (pad)"; two pads → one pad glyph (same family). Per-OWNER prompts (rescue F/E — one
    /// twin) must NOT use this; they stay single-device via <see cref="Glyph"/>/<see cref="Apply"/>.</summary>
    public static string GlyphShared(string actionName)
    {
        if (string.IsNullOrEmpty(actionName)) return actionName;

        var provider = PlayerInputRouter.SharedInput;   // path source only — kind is forced per family below
        var parts = new List<string>();
        foreach (var kind in ActiveDeviceKinds())
        {
            string stem = InputGlyphResolver.ResolveTmpSpriteNameForKind(provider, actionName, kind, out string fallback);
            if (!string.IsNullOrEmpty(stem)) parts.Add($"<sprite name=\"{stem}\">");
            else if (!string.IsNullOrEmpty(fallback)) parts.Add($"[{fallback}]");
        }
        if (parts.Count == 0) return actionName;
        return string.Join(SharedSeparator, parts);
    }

    /// <summary>Like <see cref="Format"/> but each <c>{Action}</c> token expands to ALL active device families
    /// (" | "-joined) — for shared/both-player prompts.</summary>
    public static string FormatShared(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return TokenPattern.Replace(template, m => GlyphShared(m.Groups[1].Value));
    }

    /// <summary>Shared-prompt entry point: assign the sprite asset once, then set the label to
    /// <paramref name="template"/> with every <c>{Action}</c> expanded across all active devices.</summary>
    public static void ApplyShared(TMP_Text label, string template)
    {
        if (label == null) return;
        var asset = SpriteAsset;
        if (asset != null && label.spriteAsset != asset) label.spriteAsset = asset;
        label.text = FormatShared(template);
    }

    // The distinct input-device families in play right now: each couch player's paired kind (P1 + P2 readers),
    // deduped and ordered keyboard-first so "F | (pad)" reads naturally. Solo / single-device (both unrestricted →
    // null) collapses to the live last-used family. Mirrors CouchDeviceManager's pairing (both readers get
    // SetPairedDevices in couch, so both report a non-null PairedDeviceKind).
    private static readonly List<InputDeviceKind> _kindBuffer = new List<InputDeviceKind>(2);
    private static IReadOnlyList<InputDeviceKind> ActiveDeviceKinds()
    {
        _kindBuffer.Clear();
        AddKind(PlayerInputRouter.ForSlot(PlayerSlot.One)?.PairedDeviceKind);
        AddKind(PlayerInputRouter.ForSlot(PlayerSlot.Two)?.PairedDeviceKind);
        if (_kindBuffer.Count == 0) _kindBuffer.Add(LastUsedDeviceTracker.LastUsed);   // solo → last-used
        // Keyboard before Gamepad (F | pad).
        if (_kindBuffer.Count == 2 && _kindBuffer[0] == InputDeviceKind.Gamepad)
            (_kindBuffer[0], _kindBuffer[1]) = (_kindBuffer[1], _kindBuffer[0]);
        return _kindBuffer;
    }

    private static void AddKind(InputDeviceKind? kind)
    {
        if (kind.HasValue && !_kindBuffer.Contains(kind.Value)) _kindBuffer.Add(kind.Value);
    }
}
