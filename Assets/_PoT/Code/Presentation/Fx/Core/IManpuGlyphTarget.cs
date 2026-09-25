using UnityEngine;

/// <summary>
/// P19 seam 2: what a Manpu cue element needs from the target's glyph slot,
/// declared package-side so <c>FxManager</c>'s element dispatch never names <c>ManpuSlot</c>
/// directly. <c>ManpuSlot</c> implements this; FxManager resolves it with
/// <c>GetComponentInChildren&lt;IManpuGlyphTarget&gt;</c> on the cue's follow target.
/// </summary>
public interface IManpuGlyphTarget
{
    /// <summary>Pulse a cue-authored glyph (transient ~1s; dropped if an ability owns the slot).</summary>
    void RequestCuePulse(Sprite sprite, Color colorA, Color colorB);
}
