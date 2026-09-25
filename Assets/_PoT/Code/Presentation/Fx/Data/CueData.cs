using UnityEngine;

/// <summary>Whether a cue's timers obey <c>Time.timeScale</c> (R10). The prefab is the source of
/// truth — <see cref="FromPrefab"/> defers to what the artist authored.</summary>
public enum TimeMode
{
    /// <summary>Default — defer to the prefab: leave <c>main.useUnscaledTime</c> exactly as authored.</summary>
    FromPrefab,
    /// <summary>Override: obey Time.timeScale — Setsuna (0.15) slows the effect.</summary>
    Scaled,
    /// <summary>Override: real time — pause-proof, for UI / anything that must move at timeScale 0.</summary>
    Unscaled,
}

/// <summary>How a spawned visual instance tracks its <c>CueContext</c> (defer to the prefab).</summary>
public enum FxAttachMode
{
    /// <summary>Default — read <c>main.simulationSpace</c>: Local→Follow, World→World. Never re-authored.</summary>
    FromPrefab,
    /// <summary>Fire and forget at the context position — does not follow anything.</summary>
    World,
    /// <summary>Follows the context's <c>followTarget</c>; reclaimed if the target dies.</summary>
    Follow,
    /// <summary>Follows the target, but if the target dies the instance detaches and finishes in place.</summary>
    FollowDetachOnTargetDeath,
    /// <summary>Follows the target's POSITION only — never inherits its rotation (a stun/possess ring must
    /// not spin with the player — BUG-060/061). Offset is world-axis; the spawn orientation is kept for the
    /// cue's whole life. Reclaimed if the target dies.</summary>
    FollowPositionOnly,
}

/// <summary>
/// Base of the cue ScriptableObject family. CONFIG ONLY — no runtime state
/// ever lives on a cue (R7); handles, voices and sequence cursors live in the managers.
/// </summary>
public abstract class CueData : ScriptableObject
{
    [Tooltip("FromPrefab (default) = don't touch the ParticleSystem's useUnscaledTime — the artist " +
             "already set it. Scaled/Unscaled override it deliberately (comment the choice, R10).")]
    public TimeMode timeMode = TimeMode.FromPrefab;

    /// <summary>
    /// Nominal duration used for sequence scheduling. 0 = instantaneous / unknown / looping (a
    /// following step will not wait on it).
    /// </summary>
    public abstract float GetDuration();
}
