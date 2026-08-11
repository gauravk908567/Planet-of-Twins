using UnityEngine;

/// <summary>
/// Marker for a particle system whose FOOTPRINT size should track a gameplay radius at runtime (a knockback ring,
/// a shockwave wave, an AOE floor) — driven by <see cref="CueContext.emitterScale"/>. FxManager finds these in a
/// spawned cue and scales each one's <c>startSize</c> + <c>shape.radius</c> by the requested factor; decorative
/// systems (glow, dust) are left UNMARKED so they keep their authored size (the industry parameter-driven-scale
/// pattern — never transform-scale, which blows up the decoration).
///
/// Pool-safe: the prefab's authored base is cached once, and each spawn SETS base × scale (never accumulates),
/// so a reused pooled instance never compounds a previous cue's size (F2).
///
/// Put this on the ONE (or few) systems that define the visible footprint. No marker on a prefab + a non-1
/// emitterScale = FxManager logs a warning and leaves the size unchanged (fail loud).
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class CueScalableEmitter : MonoBehaviour
{
    private ParticleSystem _ps;
    private float _baseSizeMultiplier;
    private float _baseRadius = -1f;   // <0 = shape disabled / no radius to scale
    private bool _cached;

    private void Awake() => Cache();

    private void Cache()
    {
        if (_cached) return;
        _ps = GetComponent<ParticleSystem>();
        var main = _ps.main;
        _baseSizeMultiplier = main.startSizeMultiplier;   // multiplier scales the whole startSize curve, mode-agnostic
        var shape = _ps.shape;
        _baseRadius = shape.enabled ? shape.radius : -1f;
        _cached = true;
    }

    /// <summary>Set footprint size to base × <paramref name="scale"/>. Called by FxManager on each spawn.</summary>
    public void ApplyScale(float scale)
    {
        if (!_cached) Cache();
        if (scale <= 0f) scale = 1f;

        var main = _ps.main;
        main.startSizeMultiplier = _baseSizeMultiplier * scale;

        if (_baseRadius >= 0f)
        {
            var shape = _ps.shape;
            shape.radius = _baseRadius * scale;
        }
    }
}
