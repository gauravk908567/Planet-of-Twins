using UnityEngine;

/// <summary>
/// Tutorial input gate. Implements both IInputProvider (legacy, kept for
/// any existing references) and ITutorialGate (new — used by TwinInputReader).
///
/// TwinInputReader now owns all input. Wire tutorialGateMono on TwinInputReader
/// to this component. All dispatchers stay wired to TwinInputReader permanently.
///
/// SETUP:
///   Keep on TutorialManager GO.
///   Wire TwinInputReader.tutorialGateMono → this component.
///   All dispatchers wire inputProviderObject → TwinInputReader (PlayerManager).
/// </summary>
public class TutorialInputGate : MonoBehaviour, IInputProvider, ITutorialGate
{
    private TwinInputReader _reader;
    private IInputProvider _real;

    // ── Per-action locks ──────────────────────────────────────
    private bool _attackAllowed = false;
    private bool _abilityAllowed = false;
    private bool _teleportAllowed = false;
    private bool _rescueAllowed = false;
    private bool _switchAllowed = false;
    private bool _interactAllowed = false;

    private void Start()
    {
        _reader = TwinInputReader.Instance;
        _real = _reader;
        if (_reader == null)
            Debug.LogError("[TutorialInputGate] TwinInputReader.Instance is null — is Persistent loaded?", this);
        // OnEnable fires before Start, so register the gate here once the reader is resolved.
        _reader?.SetGate(this);
    }

    private void OnEnable()
    {
        if (_reader != null) _reader.SetGate(this);
    }

    private void OnDisable()
    {
        if (_reader != null) _reader.SetGate(null);
    }

    // ── ITutorialGate — read by TwinInputReader ───────────────
    public bool IsAttackAllowed => _attackAllowed;
    public bool IsAbilityAllowed => _abilityAllowed;
    public bool IsTeleportAllowed => _teleportAllowed;
    public bool IsRescueAllowed => _rescueAllowed;
    public bool IsSwitchAllowed => _switchAllowed;
    public bool IsInteractAllowed => _interactAllowed;

    // ── Unlock API — called by TutorialDirector steps ─────────
    public void AllowAttack(bool allow) => _attackAllowed = allow;
    public void AllowAbility(bool allow) => _abilityAllowed = allow;
    public void AllowTeleport(bool allow) => _teleportAllowed = allow;
    public void AllowRescue(bool allow) => _rescueAllowed = allow;
    public void AllowInteract(bool allow) => _interactAllowed = allow;
    public void AllowSwitch(bool allow) => _switchAllowed = allow;
    public void AllowAll()
    {
        _attackAllowed = _abilityAllowed = _teleportAllowed =
        _rescueAllowed = _switchAllowed = _interactAllowed = true;
    }

    public void LockAll()
    {
        _attackAllowed = _abilityAllowed = _teleportAllowed =
        _rescueAllowed = _switchAllowed = _interactAllowed = false;
    }

    // ── IInputProvider — legacy, passthrough with gate ────────
    // Kept so any existing references to this as IInputProvider still work.
    public Vector2 GetMovementInput() => _real?.GetMovementInput() ?? Vector2.zero;
    public Vector3 GetMovementDirection() => _real?.GetMovementDirection() ?? Vector3.zero;
    public bool GetAttackDown() => _attackAllowed && (_real?.GetAttackDown() ?? false);
    public bool GetSwitchDown() => _switchAllowed && (_real?.GetSwitchDown() ?? false);
    public bool GetAbilityDown() => _abilityAllowed && (_real?.GetAbilityDown() ?? false);
    public bool GetTeleportHeld() => _teleportAllowed && (_real?.GetTeleportHeld() ?? false);
    public bool GetTeleportReleased() => _teleportAllowed && (_real?.GetTeleportReleased() ?? false);
    public bool GetRescueMash() => _rescueAllowed && (_real?.GetRescueMash() ?? false);
    public bool GetInteractDown() => _interactAllowed && (_real?.GetInteractDown() ?? false);
    public bool GetCancelHeld() => _real?.GetCancelHeld() ?? false;
    public bool GetEmpowerHeld() => _abilityAllowed && (_real?.GetEmpowerHeld() ?? false);
    public bool GetStruggleMash() => _real?.GetStruggleMash() ?? false;
    public bool GetSoulBreakMash() => _real?.GetSoulBreakMash() ?? false;
    public bool GetConvergenceHeld() => _abilityAllowed && (_real?.GetConvergenceHeld() ?? false);

    // ── P13 additions — ungated passthroughs (mirrors TwinInputReader: these never
    //    belonged to a gate category; the 6 categories above are the locked seam) ──
    public bool GetOverviewDown() => _real?.GetOverviewDown() ?? false;
    public bool GetOverviewHeld() => _real?.GetOverviewHeld() ?? false;
    public void SetGameplayFrozen(bool frozen) => _real?.SetGameplayFrozen(frozen);
    public bool GetPauseDown() => _real?.GetPauseDown() ?? false;
    public bool GetSkillTreeToggleDown() => _real?.GetSkillTreeToggleDown() ?? false;
    public bool GetQTEMashDown() => _real?.GetQTEMashDown() ?? false;
    public bool GetAnySkipDown() => _real?.GetAnySkipDown() ?? false;
    public bool GetHintsToggleDown() => _real?.GetHintsToggleDown() ?? false;

    // F5 — binding display is device-config, not gated; passthrough to the real reader.
    public string GetBindingDisplay(string actionName, bool preferGamepad = false)
        => _real?.GetBindingDisplay(actionName, preferGamepad) ?? "?";

    // F7 — binding reset is device-config, not gated; passthrough to the real reader.
    public void ResetBindingsToDefault() => _real?.ResetBindingsToDefault();
}