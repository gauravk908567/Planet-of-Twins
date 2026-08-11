using UnityEngine;
using System.Collections;

/// <summary>
/// Sole input dispatcher for all twin abilities.
///
/// Teleport flow:
///   1. Emergency condition is met (EmergencyTeleportMonitor.IsEmergencyAvailable)
///   2. Player holds C → marker preview shows, updates every frame
///   3. Player releases C → preview hides, soul launches to marker position
///   4. Soul arrives → TeleportAbility opens cancel window, fires OnCancelWindowOpened
///   5. Player holds X → cancel progress accumulates via NotifyCancelInput
///   6. Player releases X → progress resets via NotifyCancelReleased
///   7. If X held for 0.75s → TeleportAbility.ForceEnd() → cooldown starts, soul returns
///
/// EmergencyTeleportMonitor is a READ-ONLY condition gate — this dispatcher
/// reads IsEmergencyAvailable and owns all teleport input. The monitor owns
/// no input handling whatsoever.
/// </summary>
public class TwinAbilityDispatcher : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputProviderObject;
    [SerializeField] private MonoBehaviour twinSelectorObject;
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private EmergencyTeleportMonitor emergencyMonitor;
    [Tooltip("Drag AccordStateSystem here — used to check IsAccordActive for Q routing.")]
    [SerializeField] private MonoBehaviour accordModeProviderObject;

    private IInputProvider _input;
    private IAccordModeProvider _accordMode;
    private ITwinSelector _selector;
    private AbilityController _currentAbilityController;
    private AbilityController _leftController;
    private AbilityController _rightController;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        _selector = twinSelectorObject as ITwinSelector;
        _accordMode = accordModeProviderObject as IAccordModeProvider;
    }

    private void OnEnable()
    {
        if (_selector != null)
            _selector.OnTwinSelected += OnTwinSelected;
    }

    private void OnDisable()
    {
        if (_selector != null)
            _selector.OnTwinSelected -= OnTwinSelected;
    }

    private void Start()
    {
        if (leftTwin != null) _leftController = leftTwin.GetComponent<AbilityController>();
        if (rightTwin != null) _rightController = rightTwin.GetComponent<AbilityController>();
        StartCoroutine(ResolveInitialController());
    }

    private IEnumerator ResolveInitialController()
    {
        yield return null;
        if (_selector?.SelectedTransform != null)
            _currentAbilityController = _selector.SelectedTransform.GetComponent<AbilityController>();
        else
            Debug.LogError("[TwinAbilityDispatcher] Could not resolve initial controller.", this);
    }

    private void OnTwinSelected(Transform t)
    {
        _currentAbilityController = t?.GetComponent<AbilityController>();
    }

    private void Update()
    {
        if (_input == null) return;

        // ── Primary ability (Q) ───────────────────────────────
        // During Accord: each twin has its own accord Q (VoidStrike/RadiantSeeker)
        // routed to their own AbilityController. Fire on BOTH so each twin's
        // accord ability activates from a single Q press.
        // Outside Accord: only the selected twin's controller fires (existing behaviour).
        if (_input.GetAbilityDown())
        {
            // During Accord and normal mode — only the selected twin's Q fires.
            // Each twin has its own accord ability (VoidStrike on Kai, RadiantSeeker on Lyra).
            // Player must switch with Shift to use the other twin's ability, same as normal mode.
            _currentAbilityController?.ActivatePrimary();
        }

        // ── Teleport (C) ──────────────────────────────────────
        bool emergencyAvailable = emergencyMonitor != null && emergencyMonitor.IsEmergencyAvailable;

        // Always hide preview on C release regardless of emergency state —
        // prevents marker getting stuck when rescue fires mid-hold
        if (_input.GetTeleportReleased())
        {
            _leftController?.HideTeleportPreview();
            _rightController?.HideTeleportPreview();

            if (emergencyAvailable && _currentAbilityController != null)
                _currentAbilityController.ActivateTeleportEmergency();
        }

        if (_input.GetTeleportHeld() && emergencyAvailable && _currentAbilityController != null)
            _currentAbilityController.ShowTeleportPreview();

        // Force hide preview if emergency is no longer available mid-hold
        // (e.g. health recovered above threshold while C was held)
        if (!emergencyAvailable && !_input.GetTeleportHeld())
        {
            _leftController?.HideTeleportPreview();
            _rightController?.HideTeleportPreview();
        }

        // ── Teleport cancel window (X) ────────────────────────
        // Driven by TeleportAbility's IsCancelWindowOpen — no-op when closed.
        // Both controllers checked because either twin could have cast the gate.
        bool cancelHeld = _input.GetCancelHeld();

        TeleportAbility leftTA = _leftController?.GetTeleportAbility();
        TeleportAbility rightTA = _rightController?.GetTeleportAbility();

        if (leftTA != null && leftTA.IsCancelWindowOpen)
        {
            if (cancelHeld) leftTA.NotifyCancelInput(Time.deltaTime);
            else leftTA.NotifyCancelReleased();
        }

        if (rightTA != null && rightTA.IsCancelWindowOpen)
        {
            if (cancelHeld) rightTA.NotifyCancelInput(Time.deltaTime);
            else rightTA.NotifyCancelReleased();
        }
    }
}