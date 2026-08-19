/// <summary>
/// Couch co-op (M3.2) — synchronized-hold tracker for ONE joint ability (a power that requires
/// BOTH players to engage the activation input "together"). Generic: one instance per joint
/// consumer (Accord-entry X-hold, Soul-Convergence/Setsuna F-hold, Accord-Spirits key-hold) ticks
/// its own two per-player reads each frame. Each consumer owns its instance AND its own leniency
/// value (a serialized field alongside its other timings), so the same physical key can be solo for
/// one ability and joint for another, and every joint power tunes its press-together feel separately.
///
/// Leniency model (window = leniency seconds, UNSCALED time — the sync is real-world "press
/// together", so it is Setsuna/pause-proof):
///   • Both engage within `window` of each other  → ENGAGED (stays engaged while both hold).
///   • One holds alone longer than `window`        → that attempt EXPIRES; a late partner will
///     NOT engage it. Both must release (→ none) to re-arm, then re-press within `window`.
///   • Either releases while engaged               → disengages immediately (owning ability's
///     charge resets), but re-holding within `window` re-syncs (tolerates brief input blips).
///   • Single-device fallback (both reads = the same provider, P2→P1) → p1==p2 → engages on one
///     press, so joint abilities degrade to solo when only one device is paired.
/// Pure/deterministic → covered by JointAbilityGateSelfTest (Planet of Twins Tools ▸ Couch).
/// </summary>
public sealed class JointHoldSync
{
    private bool _engaged;
    private float _soloTimer;   // continuous seconds exactly ONE side has been held (unscaled)
    private bool _expired;      // solo hold outran the window → wait for full release to re-arm

    public bool IsEngaged => _engaged;

    /// <summary>
    /// Feed both players' current activation-hold reads. Returns true while jointly engaged.
    /// </summary>
    /// <param name="dt">UNSCALED delta (the sync window is real-world time — do not pass scaled).</param>
    public bool Tick(bool p1Held, bool p2Held, float leniencyWindow, float dt)
    {
        bool both = p1Held && p2Held;
        bool none = !p1Held && !p2Held;

        if (none) { _engaged = false; _soloTimer = 0f; _expired = false; return false; }

        if (both)
        {
            if (!_engaged && _expired) return false;  // one side missed the sync window
            _engaged = true;
            _soloTimer = 0f;                          // full-window grace for a later blip
            return true;
        }

        // exactly one side held
        _engaged = false;
        _soloTimer += dt;
        if (_soloTimer > leniencyWindow) _expired = true;
        return false;
    }

    /// <summary>Hard reset — call when the owning ability force-ends / disables.</summary>
    public void Reset() { _engaged = false; _soloTimer = 0f; _expired = false; }
}
