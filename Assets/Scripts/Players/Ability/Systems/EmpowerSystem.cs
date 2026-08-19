using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// EmpowerSystem — implements IAbilityActiveState so AccordStateSystem
/// can block Accord activation while the anchored duration is running.
///
/// IsAbilityActive = true only during EmpowerState.Active.
/// Charging, cooldown, and idle all return false.
/// </summary>
public class EmpowerSystem : MonoBehaviour, IAbilityActiveState, IAbilityHUDSource
{
    [Header("Twins")]
    [SerializeField] private Player _leftTwin;
    [SerializeField] private Player _rightTwin;

    [Header("Inject")]
    [SerializeField] private MonoBehaviour _inputProviderObject;
    [SerializeField] private MonoBehaviour _twinSelectorObject;
    [SerializeField] private MonoBehaviour _rescueActiveObject;
    [Tooltip("Drag AccordStateSystem — blocks Empower while Accord is active (R claimed by AccordSpiritSystem).")]
    [SerializeField] private MonoBehaviour _accordModeObject;
    [SerializeField] private MonoBehaviour _unlockStateMono;
    [SerializeField] private MonoBehaviour _dataStoreMono;

    [Header("Activation")]
    [SerializeField] private float _chargeHoldTime = 0.75f;

    [Header("Cancel (hold X)")]
    [SerializeField] private float _cancelThreshold = 0.75f;

    [Header("Empower buffs")]
    [Tooltip("1.20 = +20% speed")]
    [SerializeField] private float _speedMultiplier = 1.20f;
    [Tooltip("1.35 = +35% damage")]
    [SerializeField] private float _damageMultiplier = 1.35f;

    [Header("Dash (Shift during Empower)")]
    [SerializeField] private float _dashSpeed = 12f;
    [SerializeField] private float _dashDuration = 0.18f;
    [SerializeField] private float _dashCooldown = 1.25f;

    [Header("HUD UI")]
    [SerializeField] private UnityEngine.UI.Slider _dashSlider;
    [SerializeField] private Color _dashReadyColour = new Color(0.4f, 0.8f, 1f, 1f);
    [SerializeField] private Color _dashCooldownColour = new Color(0.3f, 0.3f, 0.4f, 1f);

    [Header("Enemy layer")]
    [SerializeField] private LayerMask _enemyLayer;

    [Header("VFX")]
    // Book pulled from PlayerVfxLibrary.Empower via VfxLibraryProvider (R4, resolved lazily at play time).
    private CueBookData _cueBook;
    private CueHandle _buffHandle;   // held empower_buff — Follows the empowered twin for the whole window
    [Tooltip("Local position offset for the cue on the anchoring twin.")]
    [SerializeField] private Vector3 _particleSpawnOffset = new Vector3(0f, 1f, 0f);

    // ── Resolved interfaces ───────────────────────────────────
    private IInputProvider _input;
    private ISelectionLock _selectionLock;
    private ITwinSelector _twinSelector;
    private IRescueActive _rescueActive;
    private IAccordModeProvider _accordMode;
    private ISkillUnlockState _unlockState;
    private IAbilityDataStore _dataStore;

    // ── State machine ─────────────────────────────────────────
    private enum EmpowerState { Idle, Charging, Active, Cooldown }
    private EmpowerState _state = EmpowerState.Idle;

    private float _chargeProgress = 0f;
    private float _activeTimer = 0f;
    private float _cooldownTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private float _cancelProgress = 0f;

    private Player _anchoringTwin;
    private Player _empoweredTwin;
    private Player _pendingCaster;   // couch M3.4 — the twin whose player is charging Empower (the caster)
    private Coroutine _pulseCoroutine;

    private Action _onAnchorDied;
    private Action _onEmpoweredDied;

    // ── IAbilityActiveState ───────────────────────────────────
    /// <summary>
    /// True during the anchored active window.
    /// AccordStateSystem reads this to block Accord while Empower is running.
    /// </summary>
    public bool IsAbilityActive => _state == EmpowerState.Active;

    // ── IAbilityHUDSource ─────────────────────────────────────
    // Plugs into the existing AbilityIconUI — same pattern as SC.
    // Cooldown ring shows: charge progress while charging,
    //                      cooldown refill while on cooldown,
    //                      full (ready) otherwise.
    public string AbilityName => "Empower";
    public int CurrentCharges => 1;
    public int MaxCharges => 1;

    public float CooldownProgress
    {
        get
        {
            if (_state == EmpowerState.Charging) return _chargeProgress;
            if (_state == EmpowerState.Cooldown)
                return Mathf.Clamp01(1f - (_cooldownTimer / Cooldown));
            return 1f;
        }
    }

    // DashCooldownProgress: 0 = just used (empty), 1 = ready (full)
    public float DashCooldownProgress => _dashCooldown > 0f
        ? Mathf.Clamp01(1f - (_dashCooldownTimer / _dashCooldown)) : 1f;



    // ── SO-driven values ──────────────────────────────────────
    private float ActiveDuration => EmpowerData?.CurrentEmpowerDuration ?? 6f;
    private float Cooldown => EmpowerData?.CurrentEmpowerCooldown ?? 12f;
    private float KnockbackInterval => EmpowerData?.CurrentEmpowerKnockbackInterval ?? 1.5f;
    private float KnockbackForce => EmpowerData?.CurrentEmpowerKnockbackForce ?? 8f;
    private float KnockbackRadius => EmpowerData?.CurrentEmpowerKnockbackRadius ?? 5f;
    private AbilityUpgradeData EmpowerData => _dataStore?.EmpowerData;

    // ── Public accessors (HUD) ────────────────────────────────
    public bool IsActive => _state == EmpowerState.Active;
    public bool IsCharging => _state == EmpowerState.Charging;
    public float ChargeProgress => _chargeProgress;
    public float ActiveTimeRemaining => Mathf.Max(0f, ActiveDuration - _activeTimer);
    public float ActiveProgress => ActiveDuration > 0f
                                        ? Mathf.Clamp01(_activeTimer / ActiveDuration) : 1f;
    public float CancelProgress => _cancelThreshold > 0f
                                        ? Mathf.Clamp01(_cancelProgress / _cancelThreshold) : 0f;
    public bool DashReady => _dashCooldownTimer <= 0f;

    public event Action OnEmpowerStarted;
    public static EmpowerSystem Instance { get; private set; }

    public event Action OnEmpowerEnded;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _input = _inputProviderObject as IInputProvider;
        _selectionLock = _twinSelectorObject as ISelectionLock;
        _twinSelector = _twinSelectorObject as ITwinSelector;
        _rescueActive = _rescueActiveObject as IRescueActive;
        _accordMode = _accordModeObject as IAccordModeProvider;
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _dataStore = _dataStoreMono as IAbilityDataStore;

        if (_input == null) Debug.LogError("[EmpowerSystem] Missing IInputProvider", this);
        if (_selectionLock == null) Debug.LogError("[EmpowerSystem] Missing ISelectionLock", this);
        if (_twinSelector == null) Debug.LogError("[EmpowerSystem] Missing ITwinSelector", this);
        if (_dataStore == null) Debug.LogError("[EmpowerSystem] Missing IAbilityDataStore", this);
    }

    private void Update()
    {
        if (_input == null) return;

        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        // Dash slider ticks every frame regardless of state
        if (_dashSlider != null)
            _dashSlider.value = DashCooldownProgress;

        if (_dashSlider != null)
        {
            _dashSlider.value = DashCooldownProgress;

            // Change fill colour based on state
            var fill = _dashSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
            if (fill != null)
                fill.color = DashReady
                    ? _dashReadyColour   // blue — ready
                    : _dashCooldownColour; // grey — on cooldown
        }

        switch (_state)
        {
            case EmpowerState.Idle: HandleIdle(); break;
            case EmpowerState.Charging: HandleCharging(); break;
            case EmpowerState.Active: HandleActive(); break;
            case EmpowerState.Cooldown: HandleCooldown(); break;
        }
    }

    private void OnDisable()
    {
        if (_state != EmpowerState.Active) return;
        if (_anchoringTwin == null || _empoweredTwin == null)
        {
            if (_pulseCoroutine != null) { StopCoroutine(_pulseCoroutine); _pulseCoroutine = null; }
            _state = EmpowerState.Idle;
            return;
        }
        EndAbility();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── State: Idle ───────────────────────────────────────────
    private void HandleIdle()
    {
        if (!IsUnlocked()) return;
        if (_rescueActive != null && _rescueActive.IsRescueActive) return;
        if (_accordMode != null && _accordMode.IsAccordActive) return;
        Player caster = DetectEmpowerCaster();
        if (caster == null) return;

        _pendingCaster = caster;
        _chargeProgress = 0f;
        _state = EmpowerState.Charging;
    }

    // ── State: Charging ───────────────────────────────────────
    private void HandleCharging()
    {
        if (!CasterEmpowerHeld())
        {
            _chargeProgress = 0f;
            _state = EmpowerState.Idle;
            return;
        }

        if (_rescueActive != null && _rescueActive.IsRescueActive)
        {
            _chargeProgress = 0f;
            _state = EmpowerState.Idle;
            return;
        }

        _chargeProgress += Time.deltaTime / _chargeHoldTime;
        if (_chargeProgress >= 1f) { _chargeProgress = 1f; Activate(); }
    }

    // ── State: Active ─────────────────────────────────────────
    private void HandleActive()
    {
        _activeTimer += Time.deltaTime;

        // Couch M3.4: the anchored CASTER holds X to cancel their own Empower (read from the caster's provider).
        if (PlayerInputRouter.For(_anchoringTwin)?.GetCancelHeld() ?? false)
        {
            _cancelProgress += Time.deltaTime;
            if (_cancelProgress >= _cancelThreshold)
            {
                _cancelProgress = 0f;
                EndAbility();
                return;
            }
        }
        else
        {
            _cancelProgress = 0f;
        }

        // Couch M3.4: the buffed PARTNER dashes with their own Shift (GetSwitchDown), read from the
        // empowered twin's provider. Selection is locked during the window, so Shift means dash, not switch.
        if ((PlayerInputRouter.For(_empoweredTwin)?.GetSwitchDown() ?? false) && _dashCooldownTimer <= 0f)
        {
            _empoweredTwin.Movement.StartDash(_dashSpeed, _dashDuration);
            _dashCooldownTimer = _dashCooldown;
        }

        if (_activeTimer >= ActiveDuration)
            EndAbility();
    }

    // ── State: Cooldown ───────────────────────────────────────
    private void HandleCooldown()
    {
        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer <= 0f)
        {
            _cooldownTimer = 0f;
            _state = EmpowerState.Idle;
        }
    }

    // ── Activate ──────────────────────────────────────────────
    private void Activate()
    {
        Player selected = _pendingCaster;   // couch M3.4 — the caster (set in HandleIdle), not the selected twin
        if (selected == null)
        {
            Debug.LogWarning("[EmpowerSystem] Could not resolve the Empower caster.", this);
            _chargeProgress = 0f;
            _state = EmpowerState.Idle;
            return;
        }

        _anchoringTwin = selected;
        _empoweredTwin = selected == _leftTwin ? _rightTwin : _leftTwin;
        _state = EmpowerState.Active;
        _activeTimer = 0f;
        _chargeProgress = 0f;
        _cancelProgress = 0f;
        _dashCooldownTimer = 0f;

        _anchoringTwin.Movement.SetMovementLocked(true);
        _anchoringTwin.GetComponent<AbilityController>()?.LockAbilities();
        _selectionLock?.LockSelection();
        _twinSelector?.ForceSelect(_empoweredTwin);

        _empoweredTwin.Movement.SetSpeedMultiplier(_speedMultiplier);
        _empoweredTwin.GetComponent<PlayerAttackController>()
            ?.SetEmpowerMultiplier(_damageMultiplier);

        // Held empower_buff aura — Follows the empowered twin for the whole window (stopped in EndAbility).
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Empower;   // R4
        if (_cueBook != null)
 // Tier-resolved: plays empower_buff_t[n] when authored in the book, else the base id.
            _buffHandle = FxManager.Instance?.PlayBook(_cueBook,
                UpgradeCueResolver.Resolve(_cueBook, EmpowerData, FxIds.Player.Empower.empower_buff),
                CueContext.Follow(_empoweredTwin.transform)) ?? CueHandle.None;

        _pulseCoroutine = StartCoroutine(KnockbackPulse());

        _onAnchorDied = () => { if (_state == EmpowerState.Active) EndAbility(); };
        _onEmpoweredDied = () => { if (_state == EmpowerState.Active) EndAbility(); };
        _anchoringTwin.Health.OnDeath += _onAnchorDied;
        _empoweredTwin.Health.OnDeath += _onEmpoweredDied;

        OnEmpowerStarted?.Invoke();
    }

    public void ForceEnd() => EndAbility();

    // ── End ───────────────────────────────────────────────────
    private void EndAbility()
    {
        if (_state != EmpowerState.Active) return;

        FxManager.Instance?.Stop(_buffHandle);   // stop the held empower_buff aura
        _buffHandle = CueHandle.None;

        if (_anchoringTwin != null)
        {
            _anchoringTwin.Movement.SetMovementLocked(false);
            _anchoringTwin.GetComponent<AbilityController>()?.UnlockAbilities();
        }

        _selectionLock?.UnlockSelection();

        if (_empoweredTwin != null)
        {
            _empoweredTwin.Movement.SetSpeedMultiplier(1f);
            _empoweredTwin.GetComponent<PlayerAttackController>()?.SetEmpowerMultiplier(1f);
        }

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        if (_anchoringTwin != null) _anchoringTwin.Health.OnDeath -= _onAnchorDied;
        if (_empoweredTwin != null) _empoweredTwin.Health.OnDeath -= _onEmpoweredDied;
        _onAnchorDied = null;
        _onEmpoweredDied = null;

        _cancelProgress = 0f;
        _cooldownTimer = Cooldown;
        _state = EmpowerState.Cooldown;

        OnEmpowerEnded?.Invoke();
    }

    // ── Knockback pulse ───────────────────────────────────────
    private IEnumerator KnockbackPulse()
    {
        float interval = KnockbackInterval;
        PulseKnockback();

        while (_state == EmpowerState.Active)
        {
            yield return new WaitForSeconds(interval);
            if (_state == EmpowerState.Active)
                PulseKnockback();
        }
    }

    private void PulseKnockback()
    {
        if (_anchoringTwin == null) return;

        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Empower;  // R4 — book from PlayerVfxLibrary
        if (_cueBook != null)
        {
            Vector3 pos = _anchoringTwin.transform.position
                        + _anchoringTwin.transform.TransformDirection(_particleSpawnOffset);
            // emitterScale: ring grows with the upgraded knockback radius (base 5 → 6.6). simSpeed: fast, ~matches the push.
 // Tier-resolved: plays empower_pulse_t[n] when authored, else the base id.
            FxManager.Instance?.PlayBook(_cueBook,
                UpgradeCueResolver.Resolve(_cueBook, EmpowerData, FxIds.Player.Empower.empower_pulse),
                new CueContext(pos, emitterScale: KnockbackRadius / 5f, simSpeed: 2.5f));
        }

        Collider[] hits = Physics.OverlapSphere(
            _anchoringTwin.transform.position, KnockbackRadius, _enemyLayer);

        foreach (Collider hit in hits)
        {
            var receiver = hit.GetComponent<IKnockbackReceiver>();
            if (receiver == null) continue;

            Vector3 dir =
                (hit.transform.position - _anchoringTwin.transform.position).normalized;

            receiver.ReceiveKnockback(new KnockbackData(dir * KnockbackForce,
                                                         KnockbackSource.Empower));
        }
    }

    // ── Helpers ───────────────────────────────────────────────
    // Couch M3.4 — Empower is a per-player SOLO cast: the caster's twin anchors (pulsing knockback
    // totem), the partner's twin is buffed. Caster = the player who pressed Empower, detected per
    // provider (PlayerInputRouter). Single-device (both providers = P1) defaults the caster to the
    // left twin. Only one Empower runs at a time (single-instance system — faithful port of D1).
    private Player DetectEmpowerCaster()
    {
        if (PlayerInputRouter.For(_leftTwin)?.GetEmpowerHeld() ?? false) return _leftTwin;
        if (PlayerInputRouter.For(_rightTwin)?.GetEmpowerHeld() ?? false) return _rightTwin;
        return null;
    }

    /// <summary>The caster (the player who started the charge) still holding the Empower key.</summary>
    private bool CasterEmpowerHeld()
    {
        var p = _pendingCaster != null ? PlayerInputRouter.For(_pendingCaster) : _input;
        return (p ?? _input)?.GetEmpowerHeld() ?? false;
    }

    private bool IsUnlocked() =>
        _unlockState == null || _unlockState.IsEmpowerUnlocked;
}