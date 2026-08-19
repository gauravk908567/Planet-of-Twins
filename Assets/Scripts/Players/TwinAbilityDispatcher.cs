using UnityEngine;

/// <summary>
/// Per-player ability dispatch (couch S2). Primary (Q) and emergency Teleport (C) are SHARED-cooldown:
/// either twin's OWNING player triggers, and the slot is unavailable to BOTH until it replenishes.
///
/// <para><b>Shared availability = both twins' primary/teleport are ready.</b> Casting either (Kai=Stun/
/// VoidStrike, Lyra=Possess/RadiantSeeker) puts that ability on cooldown → the AND becomes false → the
/// slot is locked for the used ability's own cooldown. A whiff (no target) never starts a cooldown, so it
/// never false-locks. No coordinator / cooldown-syncing / HUD rewire needed — each ability keeps its own
/// cooldown; the dispatcher just gates on both.</para>
///
/// Selection is gone (dropped _currentAbilityController / OnTwinSelected). Each twin fires its own ability
/// and cancels its own teleport via <see cref="PlayerInputRouter.For"/>. Single-device (P2→P1) → both reads
/// identical → unchanged. The inputProviderObject / twinSelectorObject / accordModeProviderObject serialized
/// slots are retained for wiring stability and retired in the S5 TwinSelector teardown.
///
/// Teleport flow (unchanged, now per-owner): hold C → preview; release C → launch (emergency + shared-ready);
/// after the gate opens its cancel window, the OWNING player holds X → cancel accrues, releases X → resets.
/// </summary>
public class TwinAbilityDispatcher : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputProviderObject;        // retained for wiring (unused post-S2)
    [SerializeField] private MonoBehaviour twinSelectorObject;         // retained for wiring (unused post-S2)
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private EmergencyTeleportMonitor emergencyMonitor;
    [SerializeField] private MonoBehaviour accordModeProviderObject;   // retained for wiring (unused post-S2)

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

    // ── Primary (Q) — shared-cooldown, per-player trigger ─────
    private void HandlePrimary()
    {
        // Shared availability: castable only while BOTH primaries are ready. Casting either drops it.
        if (!(_leftController?.IsPrimaryReady ?? false) || !(_rightController?.IsPrimaryReady ?? false))
            return;

        // First presser casts their OWN primary (left wins a same-frame tie). ActivatePrimary enforces
        // that twin's own PrimaryLocked (bomb suppression) — a suppressed twin simply doesn't fire.
        if (PlayerInputRouter.For(leftTwin)?.GetAbilityDown() ?? false)
            _leftController?.ActivatePrimary();
        else if (PlayerInputRouter.For(rightTwin)?.GetAbilityDown() ?? false)
            _rightController?.ActivatePrimary();
    }

    // ── Teleport (C, emergency) — shared-cooldown, per-owner ──
    private void HandleTeleport()
    {
        bool emergencyAvailable = emergencyMonitor != null && emergencyMonitor.IsEmergencyAvailable;
        bool sharedReady = (_leftController?.IsTeleportReady ?? false)
                        && (_rightController?.IsTeleportReady ?? false);

        TeleportForTwin(leftTwin,  _leftController,  emergencyAvailable, sharedReady);
        TeleportForTwin(rightTwin, _rightController, emergencyAvailable, sharedReady);

        // Cancel window (X) — each teleport is cancelled by its OWNING player's X.
        HandleTeleportCancel(leftTwin,  _leftController);
        HandleTeleportCancel(rightTwin, _rightController);
    }

    private void TeleportForTwin(Player twin, AbilityController ctrl, bool emergencyAvailable, bool sharedReady)
    {
        var input = PlayerInputRouter.For(twin);
        if (input == null || ctrl == null) return;

        if (input.GetTeleportReleased())
        {
            ctrl.HideTeleportPreview();
            if (emergencyAvailable && sharedReady) ctrl.ActivateTeleportEmergency();
        }

        if (input.GetTeleportHeld() && emergencyAvailable && sharedReady)
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
