using System;
using UnityEngine;

/// <summary>
/// AccordStateSystem — X bar system (unlocked via skill tree).
///
/// ACTIVATION BLOCKS:
///   - Rescue active           → blocked (IRescueActive)
///   - SC power state active   → blocked (IAbilityActiveState on SoulConvergenceSystem)
///   - Empower active          → blocked (IAbilityActiveState on EmpowerSystem)
///
/// When Accord ends, SC continues independently if active.
/// SC's own VFX/buffs run to completion — AccordStateSystem never touches SC state.
///
/// FUTURE HOOK — Enhanced Weaver's Gate during Accord:
///   TeleportAbility or RescueEventController should check IAccordModeProvider.IsAccordActive
///   to determine whether to spawn the enhanced soul variant. This system fires
///   OnAccordActivated and OnAccordDeactivated — subscribe there to toggle
///   the enhanced gate behaviour. No changes needed here.
/// </summary>
public class AccordStateSystem : MonoBehaviour, IAccordModeProvider
{
    public static AccordStateSystem Instance { get; private set; }
    [Header("Twins")]
    [SerializeField] private Player _leftTwin;   // Lyra
    [SerializeField] private Player _rightTwin;  // Kai

    [Header("Teleport cancel — inject both AbilityControllers")]
    [Tooltip("Drag left twin AbilityController — blocks Accord X when teleport cancel window open.")]
    [SerializeField] private AbilityController _leftAbilityController;
    [Tooltip("Drag right twin AbilityController — blocks Accord X when teleport cancel window open.")]
    [SerializeField] private AbilityController _rightAbilityController;

    [Header("Inject")]
    [SerializeField] private MonoBehaviour _deathNotifierMono;
    [SerializeField] private MonoBehaviour _rescueActiveMono;
    [SerializeField] private MonoBehaviour _unlockStateMono;
    [SerializeField] private MonoBehaviour _dataStoreMono;
    [SerializeField] private MonoBehaviour _inputProviderMono;

    [Tooltip("Drag SoulConvergenceSystem here — blocks Accord while SC power state is active.")]
    [SerializeField] private MonoBehaviour _scActiveStateMono;

    [Tooltip("Drag EmpowerSystem here — blocks Accord while Empower active window is running.")]
    [SerializeField] private MonoBehaviour _empowerActiveStateMono;

    [Header("Fill — damage thresholds")]
    [SerializeField] private float _killPoints = 1f;
    [SerializeField] private float _hitPointsMid = 0.5f;
    [SerializeField] private float _hitPointsLow = 1.0f;
    [SerializeField] private float _hitCooldownMid = 1.0f;
    [SerializeField] private float _hitCooldownLow = 0.25f;
    [SerializeField] private float _healthThresholdMid = 0.60f;
    [SerializeField] private float _healthThresholdLow = 0.30f;

    [Header("Co-op — joint activation (D2)")]
    [Tooltip("Synchronized-start leniency (seconds, UNSCALED): the max gap between the two players' " +
             "X-holds that still counts as pressing 'together' to enter Accord. Higher = more forgiving.")]
    [SerializeField, Range(0f, 1.5f)] private float _jointLeniency = 0.5f;
    private readonly JointHoldSync _jointSync = new JointHoldSync();

    [Header("Activation timing")]
    [SerializeField] private float _chargeTime = 1.25f;
    [SerializeField] private float _damageWindowBlock = 0.70f;
    [SerializeField] private float _retryCooldown = 0.25f;

    [Header("Enemy layers")]
    [SerializeField] private LayerMask _enemyLayer;

    // AccordMelee / VoidStrike cue books are pulled from PlayerVfxLibrary by the abilities themselves (R4) —
    // not wired here. See PlayerVfxLibrary.AccordMelee / .VoidStrike.

    [Header("Accord Melee — fixed design values")]
    [SerializeField] private float _meleeRange = 5f;
    [SerializeField] private float _meleeArcDegrees = 120f;
    [SerializeField] private float _meleeSpeedMultiplier = 0.4f;
    [SerializeField] private float _meleeDamage = 20f;

    [Header("Void Strike (Kai Q) — design values")]
    [SerializeField] private float _vsSpawnRadius = 3f;
    [SerializeField] private float _vsMinDistance = 0.8f;
    [SerializeField] private float _vsSpawnInterval = 0.25f;
    [SerializeField] private float _vsDuration = 5f;
    [SerializeField] private float _vsPointRadius = 0.6f;
    [SerializeField] private float _vsDamagePerTick = 0.6f;
    [SerializeField] private float _vsTickInterval = 0.1f;
    [SerializeField] private float _vsCooldown = 4f;
    [SerializeField] private int _vsMaxPoints = 5;

    [Header("Radiant Seeker (Lyra Q) — fixed design values")]
    [SerializeField] private GameObject _seekerOrbPrefab;
    [SerializeField] private LayerMask _seekerEnemyLayer;
    [SerializeField] private float _seekerPossessionRadius = 2.5f;
    [SerializeField] private float _seekerPossessionDuration = 2f;
    [SerializeField] private float _seekerReseekCooldown = 0.55f;
    [SerializeField] private float _seekerCooldown = 5f;

    // ── Resolved interfaces ───────────────────────────────────
    private IEnemyDeathNotifier _deathNotifier;
    private IRescueActive _rescueActive;
    private ISkillUnlockState _unlockState;
    private IAbilityDataStore _dataStore;
    private IAbilityActiveState _scActiveState;
    private IAbilityActiveState _empowerActiveState;
    private IInputProvider _input;
    public IAbilityHUDSource VoidStrikeHUDSource => _voidStrike;
    public IAbilityHUDSource RadiantSeekerHUDSource => _radiantSeeker;
    public IAbilityActiveState VoidStrikeActiveState => _voidStrike;
    public IAbilityActiveState RadiantSeekerActiveState => _radiantSeeker;
    public IAbilityHUDSource AccordSpiritHUDSource => _accordSpiritSystem;

    // ── SO-driven values ──────────────────────────────────────
    private AbilityUpgradeData AccordData => _dataStore?.AccordData;
    private float ActiveDuration => AccordData?.CurrentAccordDuration ?? 12f;
    private float BarCap => AccordData?.CurrentAccordBarCap ?? 35f;
    private float ShockwaveForce => AccordData?.CurrentAccordShockwaveForce ?? 15f;
    private float MeleeSlowDuration => AccordData?.CurrentAccordMeleeSlowDuration ?? 1.25f;
    private float MeleeAggroLossDuration => AccordData?.CurrentAccordMeleeAggroLossDuration ?? 0.75f;

    // ── Bar state ─────────────────────────────────────────────
    private float _barPoints = 0f;
    private bool _barFull = false;

    // ── Charge state ──────────────────────────────────────────
    private enum ChargeState { Idle, Charging, RetryCooldown }
    private ChargeState _chargeState = ChargeState.Idle;
    private float _chargeProgress = 0f;
    private float _retryCooldownTimer = 0f;
    private bool _damageWindowClosed = false;

    // ── Active state ──────────────────────────────────────────
    private bool _isActive = false;
    private float _activeTimer = 0f;

    // ── Per-twin hit cooldowns ────────────────────────────────
    private float _leftHitCooldown = 0f;
    private float _rightHitCooldown = 0f;

    [Header("Accord Spirit System")]
    [Tooltip("Drag AccordSpiritSystem here — exposed to HUD via AccordSpiritHUDSource.")]
    [SerializeField] private AccordSpiritSystem _accordSpiritSystem;

    // ── Abilities ─────────────────────────────────────────────
    private AccordMeleeAbility _accordMelee;
    private VoidStrikeAbility _voidStrike;
    private RadiantSeekerAbility _radiantSeeker;

    private AbilityController _kaiController;
    private AbilityController _lyraController;

    // AccordState cues (PlayerVfxLibrary.AccordState, R4 lazy): per-twin charge-up (Charging) + active (accord on),
    // plus the activation shockwave burst. Kai=right/Vethara, Lyra=left/Luminari.
    private CueBookData _accordCueBook;
    private CueHandle _chargeKaiHandle, _chargeLyraHandle, _activeKaiHandle, _activeLyraHandle;
    private CueHandle _buffKaiHandle, _buffLyraHandle;   // accord_ActiveBuff held on EACH twin during active

    // ── IAccordModeProvider ───────────────────────────────────
    public bool IsAccordActive => _isActive;

    public void ExecuteAccordMelee()
    {
        if (!_isActive) return;
        _accordMelee?.Execute(_leftTwin.transform, isKai: false);   // left = Lyra (Luminari)
        _accordMelee?.Execute(_rightTwin.transform, isKai: true);   // right = Kai (Vethara)
    }

    // ── Public accessors (HUD / slider) ──────────────────────
    public float BarProgress => BarCap > 0f ? Mathf.Clamp01(_barPoints / BarCap) : 0f;
    public float ChargeProgress => _chargeProgress;
    public float ActiveTimeRemaining => Mathf.Max(0f, ActiveDuration - _activeTimer);
    public float ActiveProgress => ActiveDuration > 0f
                                        ? Mathf.Clamp01(_activeTimer / ActiveDuration) : 1f;
    public bool BarIsFull => _barFull;

    public event Action OnAccordActivated;
    public event Action OnAccordDeactivated;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _deathNotifier = _deathNotifierMono as IEnemyDeathNotifier;
        _rescueActive = _rescueActiveMono as IRescueActive;
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _dataStore = _dataStoreMono as IAbilityDataStore;
        _scActiveState = _scActiveStateMono as IAbilityActiveState;
        _empowerActiveState = _empowerActiveStateMono as IAbilityActiveState;
        _input = _inputProviderMono as IInputProvider;

        if (_deathNotifier == null)
            Debug.LogError("[AccordStateSystem] Missing IEnemyDeathNotifier", this);
        if (_dataStore == null)
            Debug.LogError("[AccordStateSystem] Missing IAbilityDataStore", this);
        if (_scActiveState == null)
            Debug.LogWarning("[AccordStateSystem] SoulConvergenceSystem not injected — SC won't block Accord.", this);
        if (_empowerActiveState == null)
            Debug.LogWarning("[AccordStateSystem] EmpowerSystem not injected — Empower won't block Accord.", this);

        _kaiController = _rightTwin?.GetComponent<AbilityController>();
        _lyraController = _leftTwin?.GetComponent<AbilityController>();

        BuildAbilities();
    }

    private void Start()
    {
        if (_input == null) _input = TwinInputReader.Instance;
        if (_input == null) Debug.LogError("[AccordStateSystem] IInputProvider not resolved — wire _inputProviderMono.", this);
    }

    private void BuildAbilities()
    {
        _accordMelee = new AccordMeleeAbility(
            this, _enemyLayer,
            _meleeRange, _meleeArcDegrees,
            MeleeAggroLossDuration, MeleeSlowDuration,
            _meleeSpeedMultiplier,
            _meleeDamage);

        // Use each twin as the runner so abilities spawn/search from the correct position.
        // VoidStrike searches from Kai's position, RadiantSeeker spawns at Lyra's position.
        _voidStrike = new VoidStrikeAbility(
            _rightTwin, _enemyLayer,
            _vsSpawnRadius, _vsMinDistance,
            _vsSpawnInterval, _vsDuration,
            _vsPointRadius, _vsDamagePerTick,
            _vsTickInterval, _vsCooldown,
            _vsMaxPoints);

        _radiantSeeker = new RadiantSeekerAbility(
            _leftTwin, _seekerOrbPrefab, _seekerEnemyLayer,
            _seekerPossessionRadius, _seekerPossessionDuration,
            _seekerReseekCooldown, _seekerCooldown);
    }

    private void OnEnable()
    {
        if (_deathNotifier != null) _deathNotifier.OnEnemyDied += HandleKill;
        if (_leftTwin != null) _leftTwin.Health.OnDamageTaken += HandleLeftDamageTaken;
        if (_rightTwin != null) _rightTwin.Health.OnDamageTaken += HandleRightDamageTaken;
        if (_unlockState != null) _unlockState.OnAccordStateUnlocked += OnUnlocked;
    }

    private void OnDisable()
    {
        if (_deathNotifier != null) _deathNotifier.OnEnemyDied -= HandleKill;
        if (_leftTwin != null) _leftTwin.Health.OnDamageTaken -= HandleLeftDamageTaken;
        if (_rightTwin != null) _rightTwin.Health.OnDamageTaken -= HandleRightDamageTaken;
        if (_unlockState != null) _unlockState.OnAccordStateUnlocked -= OnUnlocked;
        if (_isActive) DeactivateAccord();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Update ────────────────────────────────────────────────
    private void Update()
    {
        if (!IsUnlocked()) return;

        if (_leftHitCooldown > 0f) _leftHitCooldown -= Time.deltaTime;
        if (_rightHitCooldown > 0f) _rightHitCooldown -= Time.deltaTime;

        if (_isActive) { TickActive(); return; }

        switch (_chargeState)
        {
            case ChargeState.Idle: HandleIdle(); break;
            case ChargeState.Charging: HandleCharging(); break;
            case ChargeState.RetryCooldown: HandleRetryCooldown(); break;
        }
    }

    /// <summary>Both players holding the Accord key (X) together, synced within <c>_jointLeniency</c>
    /// (D2 — entering Accord State is a JOINT act; it's the gateway the other combined powers live
    /// behind). Only ticked while listening — the HandleIdle guards return before this when blocked
    /// (rescue / SC / Empower / teleport), so it never cross-talks with those X uses. Single-device
    /// (P2→P1) → engages on one press (solo). No router/roster → solo read.</summary>
    private bool JointCancelHeld()
    {
        var pa = PlayerInputRouter.For(_leftTwin);
        var pb = PlayerInputRouter.For(_rightTwin);
        if (pa == null || pb == null) return _input?.GetCancelHeld() ?? false;
        return _jointSync.Tick(pa.GetCancelHeld(), pb.GetCancelHeld(),
                               _jointLeniency, Time.unscaledDeltaTime);
    }

    // ── Idle ──────────────────────────────────────────────────
    private void HandleIdle()
    {
        if (!_barFull) return;

        // Block if rescue is active
        if (_rescueActive != null && _rescueActive.IsRescueActive) return;

        // Block if EITHER twin is down — Accord State is the CLAN-combining power and needs BOTH twins up.
        // (Soul Convergence / Setsuna / Accord Spirit are SOUL powers and are NOT gated this way.)
        if (IsEitherTwinDown()) return;

        // Block if Soul Convergence power state is running
        if (_scActiveState != null && _scActiveState.IsAbilityActive) return;

        // Block if Empower active window is running
        if (_empowerActiveState != null && _empowerActiveState.IsAbilityActive) return;

        // Block if teleport cancel window is open — X is claimed by teleport cancel
        if (IsTeleportCancelWindowOpen()) return;

        if (!JointCancelHeld()) return;

        _chargeProgress = 0f;
        _damageWindowClosed = false;
        _chargeState = ChargeState.Charging;
        StartChargeVFX();
    }

    // ── Charging ──────────────────────────────────────────────
    private void HandleCharging()
    {
        // Cancel charge if rescue becomes active mid-charge
        if (_rescueActive != null && _rescueActive.IsRescueActive)
        {
            StopChargeVFX();
            _chargeProgress = 0f;
            _chargeState = ChargeState.Idle;
            return;
        }

        // Cancel charge if a twin goes down mid-charge (Accord needs both twins up).
        if (IsEitherTwinDown())
        {
            StopChargeVFX();
            _chargeProgress = 0f;
            _chargeState = ChargeState.Idle;
            return;
        }

        // Cancel charge if teleport cancel window opens mid-charge
        if (IsTeleportCancelWindowOpen())
        {
            StopChargeVFX();
            _chargeProgress = 0f;
            _chargeState = ChargeState.Idle;
            return;
        }

        if (!JointCancelHeld())
        {
            StopChargeVFX();
            _chargeProgress = 0f;
            _chargeState = ChargeState.RetryCooldown;
            _retryCooldownTimer = _retryCooldown;
            return;
        }

        _chargeProgress += Time.deltaTime / _chargeTime;

        if (!_damageWindowClosed &&
            _chargeProgress >= (_damageWindowBlock / _chargeTime))
            _damageWindowClosed = true;

        if (_chargeProgress >= 1f)
        {
            _chargeProgress = 1f;
            ActivateAccord();
        }
    }

    // Accord State is the clan-combining power → both twins must be up. "Down" = dead here; a grabbed/in-rescue
    // twin is already blocked by the IsRescueActive guards above. (The soul powers — SC / Setsuna / Accord Spirit
    // — are NOT gated this way: they can still fire with one twin down.)
    private bool IsEitherTwinDown() =>
        (_leftTwin != null && _leftTwin.Health != null && _leftTwin.Health.IsDead) ||
        (_rightTwin != null && _rightTwin.Health != null && _rightTwin.Health.IsDead);

    // ── Retry cooldown ────────────────────────────────────────
    private void HandleRetryCooldown()
    {
        _retryCooldownTimer -= Time.deltaTime;
        if (_retryCooldownTimer <= 0f)
            _chargeState = ChargeState.Idle;
    }

    // ── Active tick ───────────────────────────────────────────
    private void TickActive()
    {
        _activeTimer += Time.deltaTime;
        if (_activeTimer >= ActiveDuration)
            DeactivateAccord();
    }

    // ── Kill fill ─────────────────────────────────────────────
    private void HandleKill()
    {
        if (!IsUnlocked()) return;
        if (_isActive || _barFull) return;
        AddBarPoints(_killPoints);
    }

    // ── Damage fill ───────────────────────────────────────────
    private void HandleLeftDamageTaken(PlayerHealthComponent comp, float amount, Vector3 pos)
        => HandleDamageTaken(comp, isLeft: true);

    private void HandleRightDamageTaken(PlayerHealthComponent comp, float amount, Vector3 pos)
        => HandleDamageTaken(comp, isLeft: false);

    private void HandleDamageTaken(PlayerHealthComponent comp, bool isLeft)
    {
        if (!IsUnlocked()) return;
        // Cancel charge if damage lands in first 0.7s window
        if (_chargeState == ChargeState.Charging && !_damageWindowClosed)
        {
            StopChargeVFX();
            _chargeProgress = 0f;
            _chargeState = ChargeState.RetryCooldown;
            _retryCooldownTimer = _retryCooldown;
            return;
        }

        // Ignore damage after window closes
        if (_chargeState == ChargeState.Charging && _damageWindowClosed) return;

        if (_isActive || _barFull) return;

        float cooldown = isLeft ? _leftHitCooldown : _rightHitCooldown;
        if (cooldown > 0f) return;

        float healthPct = comp.MaxHealth > 0f
            ? comp.CombatHealth / comp.MaxHealth : 1f;

        if (healthPct > _healthThresholdMid) return;

        if (healthPct <= _healthThresholdLow)
        {
            AddBarPoints(_hitPointsLow);
            if (isLeft) _leftHitCooldown = _hitCooldownLow;
            else _rightHitCooldown = _hitCooldownLow;
        }
        else
        {
            AddBarPoints(_hitPointsMid);
            if (isLeft) _leftHitCooldown = _hitCooldownMid;
            else _rightHitCooldown = _hitCooldownMid;
        }
    }

    private void AddBarPoints(float points)
    {
        _barPoints = Mathf.Min(_barPoints + points, BarCap);
        if (_barPoints >= BarCap) _barFull = true;
    }

    // ── Activation ────────────────────────────────────────────
    private void ActivateAccord()
    {
        _isActive = true;
        _activeTimer = 0f;
        _chargeState = ChargeState.Idle;
        _chargeProgress = 0f;

        StopChargeVFX();   // charge complete
        StartActiveVFX();  // per-twin held accord aura

        _kaiController?.SetPrimaryAbility(_voidStrike);
        _lyraController?.SetPrimaryAbility(_radiantSeeker);

        FireShockwave(_leftTwin.transform.position);
        FireShockwave(_rightTwin.transform.position);

        OnAccordActivated?.Invoke();
        Debug.Log("[AccordStateSystem] Accord State ACTIVATED");
    }

    public void ForceDeactivate() { if (_isActive) DeactivateAccord(); }

    // ── Deactivation ──────────────────────────────────────────
    private void DeactivateAccord()
    {
        _isActive = false;
        _barPoints = 0f;
        _barFull = false;

        StopActiveVFX();   // accord over — per-twin held aura off

        // Q abilities restored via TwinAbilitySetup.OnAccordDeactivated.
        // SC continues independently if its power state is still running —
        // this system never touches SC state on deactivation.
        OnAccordDeactivated?.Invoke();
        Debug.Log("[AccordStateSystem] Accord State DEACTIVATED");
    }

    // ── Shockwave ─────────────────────────────────────────────
    private void FireShockwave(Vector3 origin)
    {
        // Activation shockwave burst (visual only — knockback is applied below). World@origin, fired per twin.
        _accordCueBook ??= VfxLibraryProvider.Instance?.Player?.AccordState;   // R4
        if (_accordCueBook != null)
 // Tier-resolved: plays accord_Shockwave_t[n] when authored in the book, else the base id.
            // emitterScale: ring art authored at 5 m radius (KnockBackParticle family) → stretch the
            // footprint to the fixed 12 m knockback radius below. simSpeed ~matches the push.
            FxManager.Instance?.PlayBook(_accordCueBook,
                UpgradeCueResolver.Resolve(_accordCueBook, AccordData, FxIds.Player.AccordState.accord_Shockwave),
                new CueContext(origin, emitterScale: 12f / 5f, simSpeed: 2f));

        float radius = 12f; // fixed design value
        Collider[] hits = Physics.OverlapSphere(origin, radius, _enemyLayer);

        foreach (Collider hit in hits)
        {
            var receiver = hit.GetComponent<IKnockbackReceiver>();
            if (receiver == null) continue;

            Vector3 dir = (hit.transform.position - origin).normalized;
            receiver.ReceiveKnockback(
                new KnockbackData(dir * ShockwaveForce, KnockbackSource.AccordState));
        }
    }

    // ── Helpers ───────────────────────────────────────────────
    private bool IsTeleportCancelWindowOpen()
    {
        if (_leftAbilityController != null)
        {
            var ta = _leftAbilityController.GetTeleportAbility();
            if (ta != null && ta.IsCancelWindowOpen) return true;
        }
        if (_rightAbilityController != null)
        {
            var ta = _rightAbilityController.GetTeleportAbility();
            if (ta != null && ta.IsCancelWindowOpen) return true;
        }
        return false;
    }

 // ── AccordState cues (per-twin held; Tier 1) ────────
    private void StartChargeVFX()
    {
        _accordCueBook ??= VfxLibraryProvider.Instance?.Player?.AccordState;   // R4
        var fx = FxManager.Instance;
        if (_accordCueBook == null || fx == null) return;
 // Tier-resolved: each per-twin id independently tries its _t[n] variant, else its base.
        if (_rightTwin != null) _chargeKaiHandle  = fx.PlayBook(_accordCueBook,
            UpgradeCueResolver.Resolve(_accordCueBook, AccordData, FxIds.Player.AccordState.accord_ChargeUpKai),
            CueContext.Follow(_rightTwin.transform));
        if (_leftTwin  != null) _chargeLyraHandle = fx.PlayBook(_accordCueBook,
            UpgradeCueResolver.Resolve(_accordCueBook, AccordData, FxIds.Player.AccordState.accord_ChargeUpLyra),
            CueContext.Follow(_leftTwin.transform));
    }

    private void StopChargeVFX()
    {
        FxManager.Instance?.Stop(_chargeKaiHandle);
        FxManager.Instance?.Stop(_chargeLyraHandle);
        _chargeKaiHandle = _chargeLyraHandle = CueHandle.None;
    }

    private void StartActiveVFX()
    {
        _accordCueBook ??= VfxLibraryProvider.Instance?.Player?.AccordState;   // R4
        var fx = FxManager.Instance;
        if (_accordCueBook == null || fx == null) return;
 // Tier-resolved: each id independently tries its _t[n] variant, else its base.
        if (_rightTwin != null) _activeKaiHandle  = fx.PlayBook(_accordCueBook,
            UpgradeCueResolver.Resolve(_accordCueBook, AccordData, FxIds.Player.AccordState.accord_ActiveKai),
            CueContext.Follow(_rightTwin.transform));
        if (_leftTwin  != null) _activeLyraHandle = fx.PlayBook(_accordCueBook,
            UpgradeCueResolver.Resolve(_accordCueBook, AccordData, FxIds.Player.AccordState.accord_ActiveLyra),
            CueContext.Follow(_leftTwin.transform));
        // accord_ActiveBuff is a single (non-per-twin) id — held on EACH twin for the active window.
        string buffId = UpgradeCueResolver.Resolve(_accordCueBook, AccordData, FxIds.Player.AccordState.accord_ActiveBuff);
        if (_rightTwin != null) _buffKaiHandle  = fx.PlayBook(_accordCueBook, buffId, CueContext.Follow(_rightTwin.transform));
        if (_leftTwin  != null) _buffLyraHandle = fx.PlayBook(_accordCueBook, buffId, CueContext.Follow(_leftTwin.transform));
    }

    private void StopActiveVFX()
    {
        FxManager.Instance?.Stop(_activeKaiHandle);
        FxManager.Instance?.Stop(_activeLyraHandle);
        FxManager.Instance?.Stop(_buffKaiHandle);
        FxManager.Instance?.Stop(_buffLyraHandle);
        _activeKaiHandle = _activeLyraHandle = _buffKaiHandle = _buffLyraHandle = CueHandle.None;
    }

    private bool IsUnlocked() =>
        _unlockState == null || _unlockState.IsAccordStateUnlocked;

    private void OnUnlocked() =>
        Debug.Log("[AccordStateSystem] Accord State unlocked via skill tree.");
}