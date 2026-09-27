using UnityEngine;

/// <summary>
/// Persistent ambient behaviour state — survives GOAP/BT resets.
/// Stores wander state so BTActionWander can continue across replanning cycles.
///
/// ATTACH: To every enemy prefab.
/// Separate from perception — this is behavioural state not perceptual.
/// </summary>
public class EnemyAmbientState : MonoBehaviour, IEnemyReuseReset
{
    // ── Wander state — persists across BT resets ──────────
    public Vector3 WanderTarget { get; set; } = Vector3.zero;
    public bool WanderWaiting { get; set; } = false;
    public float WanderTimer { get; set; } = 0f;
    public bool WanderInit { get; set; } = false;
    public float WanderDuration { get; set; } = 0f;

    // BUG-135 — ResetState had no caller until the pool's issue-time reset.
    public void ResetForReuse() => ResetState();

    /// <summary>Reset all state — the pool calls it on reuse via <see cref="ResetForReuse"/>.</summary>
    public void ResetState()
    {
        WanderTarget = Vector3.zero;
        WanderWaiting = false;
        WanderTimer = 0f;
        WanderInit = false;
        WanderDuration = 0f;
    }
}