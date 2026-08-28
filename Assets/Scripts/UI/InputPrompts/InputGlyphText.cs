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
}
