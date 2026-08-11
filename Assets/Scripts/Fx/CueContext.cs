using UnityEngine;

/// <summary>
/// Where and how a cue plays. Readonly value type — pass by <c>in</c>.
/// A null <see cref="followTarget"/> means world-space (fire and forget at <see cref="position"/>).
/// </summary>
public readonly struct CueContext
{
    public readonly Vector3 position;
    public readonly Quaternion rotation;
    public readonly Transform followTarget;   // null = world-space
    public readonly object owner;             // optional, for diagnostics / ownership bookkeeping

    /// <summary>Optional ORIENTATION anchor, independent of <see cref="followTarget"/> (the POSITION anchor):
    /// when set, the spawned instance re-aims its forward (+Z) at this transform every frame —
    /// <c>LookRotation(faceTarget.position − effectPos)</c>, composed onto any authored localRotation. A
    /// directional drain / gaze / beam originates at the position anchor (or a fixed point) and points at a
    /// moving target. null = keep the anchor's own rotation (current behaviour). Works whether the effect is
    /// world-static or following.</summary>
    public readonly Transform faceTarget;     // null = no re-orient

    /// <summary>Uniform size multiplier on the spawned instance vs the PREFAB's authored scale. 1 = prefab size
    /// (the default — never re-author a static cue). A caller scales the cue to live data: e.g. an ability passes
    /// <c>currentRange / baseRange</c> so the VFX radius grows with range upgrades. Applied in FxManager.Place;
    /// pool-safe (set, not accumulated, on each spawn — F2).</summary>
    public readonly float scale;

    /// <summary>Footprint-only size multiplier: scales the spawned cue's <c>CueScalableEmitter</c>-marked particle
    /// systems' startSize + shape radius — NOT the transform, NOT decorative children. 1 = prefab size. A
    /// knockback/AOE passes <c>currentRadius / baseRadius</c> so the ring matches the gameplay radius while glow/dust
    /// keep their authored size (industry parameter-driven scale). Applied in FxManager.SpawnParticle; pool-safe.</summary>
    public readonly float emitterScale;

    /// <summary>Playback-speed multiplier for the whole cue — FxManager applies it to EVERY child particle system's
    /// <c>simulationSpeed</c> (and a VFX graph's <c>playRate</c>), so the parent value cascades to children. 1 =
    /// authored speed. A knockback passes (prefab time / gameplay push time) to make the visual finish in step with
    /// the push; a charge passes (natural / chargeHoldTime). Pool-safe (reads the prefab base each spawn).</summary>
    public readonly float simSpeed;

    public CueContext(Vector3 position, Quaternion rotation, Transform followTarget = null, object owner = null,
                      float scale = 1f, float emitterScale = 1f, float simSpeed = 1f, Transform faceTarget = null)
    {
        this.position = position;
        this.rotation = rotation;
        this.followTarget = followTarget;
        this.owner = owner;
        this.scale = scale <= 0f ? 1f : scale;
        this.emitterScale = emitterScale <= 0f ? 1f : emitterScale;
        this.simSpeed = simSpeed <= 0f ? 1f : simSpeed;
        this.faceTarget = faceTarget;
    }

    public CueContext(Vector3 position, Transform followTarget = null, object owner = null, float scale = 1f,
                      float emitterScale = 1f, float simSpeed = 1f, Transform faceTarget = null)
        : this(position, Quaternion.identity, followTarget, owner, scale, emitterScale, simSpeed, faceTarget) { }

    /// <summary>Play attached to a moving target (its current transform seeds the spawn pose).
    /// <paramref name="scale"/> sizes the whole instance; <paramref name="emitterScale"/> sizes only the
    /// footprint emitter; <paramref name="simSpeed"/> retimes all child systems (1 = prefab).</summary>
    public static CueContext Follow(Transform target, object owner = null, float scale = 1f, float emitterScale = 1f,
                                    float simSpeed = 1f)
        => new CueContext(target != null ? target.position : Vector3.zero,
                          target != null ? target.rotation : Quaternion.identity,
                          target, owner, scale, emitterScale, simSpeed);

    /// <summary>Play at a FIXED world point but ORIENT toward <paramref name="face"/> every frame (a beam / gaze
    /// that stays put and tracks a moving target). Position never moves; only the facing follows.</summary>
    public static CueContext Facing(Vector3 position, Transform face, object owner = null, float scale = 1f)
        => new CueContext(position, Quaternion.identity, null, owner, scale, 1f, 1f, face);

    /// <summary>Ride <paramref name="anchor"/> (position tracks it) WHILE orienting toward <paramref name="face"/>
    /// every frame — the warden-grab drain: originate on the warden, aim the beam at the grabbed twin. The
    /// element must be authored attachMode=Follow (a VFX graph defaults to World and won't ride otherwise).</summary>
    public static CueContext FollowFacing(Transform anchor, Transform face, object owner = null, float scale = 1f)
        => new CueContext(anchor != null ? anchor.position : Vector3.zero,
                          anchor != null ? anchor.rotation : Quaternion.identity,
                          anchor, owner, scale, 1f, 1f, face);
}
