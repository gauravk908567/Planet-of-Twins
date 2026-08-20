using UnityEngine;

/// <summary>
/// Per-player ability dispatch (couch). Primary (Q) and emergency Teleport (Weaver's Gate, C) are BOTH
/// PER-TWIN: each player triggers their OWN ability, gated only by that ability's OWN cooldown — one twin
/// never blocks the other. Kai=Stun/VoidStrike vs Lyra=Possess/RadiantSeeker are different abilities; the
/// teleport is the same ability but still a per-twin instance. (Empower is the ONLY shared-cooldown ability —
/// handled in EmpowerSystem, not here.)
///
/// Selection is gone (dropped _currentAbilityController / OnTwinSelected). Each twin fires/cancels its own via
/// <see cref="PlayerInputRouter.For"/>. Single-device (P2→P1) → both reads identical → both twins act together
/// (matches Attack). The old selection/accord serialized slots were removed in the S5 TwinSelector teardown.
///
/// Teleport flow (per-owner): hold C → preview; release C → launch (emergency + that twin's own teleport ready);
/// once the gate opens its cancel window, the OWNING player holds X → cancel accrues, releases X → resets.
/// </summary>
public class TwinAbilityDispatcher : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private EmergencyTeleportMonitor emergencyMonitor;

    private AbilityController _leftController;
    private AbilityController _rightController;

    private void Start()
    {
        if (leftTwin != null)  _leftController  = leftTwin.GetComponent<AbilityController>();
        if (rightTwin != null) _rightController = rightTwin.GetComponent<AbilityController>();
    }

    private void Update()
    {
        HandlePrimary();
        HandleTeleport();
    }

    // ── Primary (Q) — PER-TWIN, independent cooldown ──────────
    private void HandlePrimary()
    {
        // Kai=Stun and Lyra=Possess are DIFFERENT abilities: each player casts their OWN primary independently,
        // gated only by that ability's OWN cooldown — one caster never blocks the other (NOT shared-cooldown;
        // that model is Empower-only). ActivatePrimary still enforces each twin's own PrimaryLocked (bomb).
        if (PlayerInputRouter.For(leftTwin)?.GetAbilityDown() ?? false)
            _leftController?.ActivatePrimary();
        if (PlayerInputRouter.For(rightTwin)?.GetAbilityDown() ?? false)
            _rightController?.ActivatePrimary();
    }

    // ── Teleport (Weaver's Gate, C, emergency) — PER-TWIN, per-owner ──
    private void HandleTeleport()
    {
        bool emergencyAvailable = emergencyMonitor != null && emergencyMonitor.IsEmergencyAvailable;

        TeleportForTwin(leftTwin,  _leftController,  emergencyAvailable);
        TeleportForTwin(rightTwin, _rightController, emergencyAvailable);

        // Cancel window (X) — each teleport is cancelled by its OWNING player's X.
        HandleTeleportCancel(leftTwin,  _leftController);
        HandleTeleportCancel(rightTwin, _rightController);
    }

    private void TeleportForTwin(Player twin, AbilityController ctrl, bool emergencyAvailable)
    {
        var input = PlayerInputRouter.For(twin);
        if (input == null || ctrl == null) return;

        bool ready = ctrl.IsTeleportReady;   // this twin's OWN Weaver's Gate cooldown/active state

        if (input.GetTeleportReleased())
        {
            ctrl.HideTeleportPreview();
            if (emergencyAvailable && ready) ctrl.ActivateTeleportEmergency();
        }

        if (input.GetTeleportHeld() && emergencyAvailable && ready)
            ctrl.ShowTeleportPreview();

        // Force-hide preview if emergency ends mid-hold (e.g. health recovered above threshold).
        if (!emergencyAvailable && !input.GetTeleportHeld())
            ctrl.HideTeleportPreview();
    }

    private void HandleTeleportCancel(Player twin, AbilityController ctrl)
    {
        var ta = ctrl?.GetTeleportAbility();
        if (ta == null || !ta.IsCancelWindowOpen) return;

        bool xHeld = PlayerInputRouter.For(twin)?.GetCancelHeld() ?? false;
        if (xHeld) ta.NotifyCancelInput(Time.deltaTime);
        else ta.NotifyCancelReleased();
    }
}
