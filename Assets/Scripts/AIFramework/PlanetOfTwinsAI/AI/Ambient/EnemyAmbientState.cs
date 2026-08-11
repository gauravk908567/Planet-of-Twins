using UnityEngine;

/// <summary>
/// Persistent ambient behaviour state — survives GOAP/BT resets.
/// Stores wander state so BTActionWander can continue across replanning cycles.
///
/// ATTACH: To every enemy prefab.
/// Separate from perception — this is behavioural state not perceptual.
/// </summary>
public class EnemyAmbientState : MonoBehaviour
{
    // ── Wander state — persists across BT resets ──────────
    public Vector3 WanderTarget { get; set; } = Vector3.zero;
    public bool WanderWaiting { get; set; } = false;
    public float WanderTimer { get; set; } = 0f;
    public bool WanderInit { get; set; } = false;
    public float WanderDuration { get; set; } = 0f;

    /// <summary>Reset all state — call when enemy is pooled/returned.</summary>
    public void ResetState()
    {
        WanderTarget = Vector3.zero;
        WanderWaiting = false;
        WanderTimer = 0f;
        WanderInit = false;
        WanderDuration = 0f;
    }
}