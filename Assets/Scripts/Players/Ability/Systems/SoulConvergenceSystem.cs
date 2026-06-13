using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SoulConvergenceSystem — implements IAbilityActiveState so AccordStateSystem
/// can block Accord activation while SC power state is running.
///
/// IsAbilityActive = true only during the 7s buff window (_abilityActive).
/// Charging, idle, and cooldown states all return false.
/// </summary>
public class SoulConvergenceSystem : MonoBehaviour, IDamageMultiplier, IAbilityActiveState
{
    public float DamageOutMultiplier { get; private set; } = 1f;
    public float DamageInMultiplier { get; private set; } = 1f;

    // ── IAbilityActiveState ───────────────────────────────────
    public bool IsAbilityActive => _abilityActive;

    // ── Setsuna hooks ─────────────────────────────────────────
    public bool IsCharged => _charged;
    public bool IsAbilityRunning => _abilityActive;
    public int SoulCount => _soulCount;
    public int SoulCap => _soulCap;

    // ── Setsuna accord UI — counter text only, panel owned by SetsunaSystem ──
    [Header("HUD UI — Accord slot counter (SetsunaAccordIconUI)")]
    [Tooltip("SoulCounterText inside SetsunaAccordIconUI — mirrored from normal counter")]
    [SerializeField] private TMP_Text _accordCounterText;

    // Exposed so SetsunaSystem can display SC timer on accord panel
    public float PowerTimeRemaining => _powerTimer;

    public static SoulConvergenceSystem Instance { get; private set; }

    // Called by SetsunaSystem to force-end SC cleanly after rewind
    public void ForceDeactivate()
    {
        if (!_abilityActive) return;
        Deactivate();
    }

    [Header("Inject")]
    [SerializeField] private MonoBehaviour _unlockStateMono;
    [SerializeField] private MonoBehaviour _dataStoreMono;
    [SerializeField] private MonoBehaviour _deathNotifierMono;
    [SerializeField] private MonoBehaviour _rescueActiveMono;
    [SerializeField] private MonoBehaviour _inputProviderMono;
    [SerializeField] private SharedHealthPool _healthPool;

    private ISkillUnlockState _unlockState;
    private IAbilityDataStore _dataStore;
    private IEnemyDeathNotifier _deathNotifier;
    private IRescueActive _rescueActive;
    private IInputProvider _input;

    [Header("Base Settings")]
    [SerializeField] private int _soulCap = 10;
    [SerializeField] private float _basePowerDuration = 7f;
    [SerializeField] private float _damageOutBonus = 0.35f;
    [SerializeField] private float _damageInReduction = 0.35f;
    [SerializeField] private float _chargeHoldTime = 0.75f;
    [SerializeField] private KeyCode _activateKey = KeyCode.F;

    [Header("HUD UI")]
    [SerializeField] private TMP_Text _counterText;
    [SerializeField] private Slider _chargeBar;
    [SerializeField] private GameObject _powerStatePanel;
    [SerializeField] private TMP_Text _powerTimerText;

    [Header("VFX")]
    [SerializeField] private Player _leftTwin;
    [SerializeField] private Player _rightTwin;
    [SerializeField] private GameObject _chargeEffectPrefab;
    [SerializeField] private GameObject _shieldPrefab;

    private ParticleSystem _leftCharge;
    private ParticleSystem _rightCharge;
    private GameObject _leftShieldInstance;
    private GameObject _rightShieldInstance;

    private int _soulCount = 0;
    private bool _charged = false;
    private bool _abilityActive = false;
    private float _chargeProgress = 0f;
    private float _powerTimer = 0f;

    // ── Lifecycle ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _dataStore = _dataStoreMono as IAbilityDataStore;
        _deathNotifier = _deathNotifierMono as IEnemyDeathNotifier;
        _rescueActive = _rescueActiveMono as IRescueActive;
        _input = _inputProviderMono as IInputProvider;

        if (_unlockState == null) Debug.LogError("[SoulConv] Missing ISkillUnlockState");
        if (_dataStore == null) Debug.LogError("[SoulConv] Missing IAbilityDataStore");
        if (_deathNotifier == null) Debug.LogError("[SoulConv] Missing IEnemyDeathNotifier");

        ResetMultipliers();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (_input == null) _input = TwinInputReader.Instance;
        if (_input == null) Debug.LogError("[SoulConv] IInputProvider not resolved — wire _inputProviderMono or ensure TwinInputReader is loaded.", this);

        _chargeBar?.gameObject.SetActive(false);
        _powerStatePanel?.SetActive(false);
        RefreshCounter();

        _leftCharge = SpawnChargeVFX(_chargeEffectPrefab, _leftTwin);
        _rightCharge = SpawnChargeVFX(_chargeEffectPrefab, _rightTwin);
    }

    void OnEnable()
    {
        if (_deathNotifier != null) _deathNotifier.OnEnemyDied += HandleKill;
        if (_unlockState != null) _unlockState.OnSoulConvergenceUnlocked += OnUnlocked;
    }

    void OnDisable()
    {
        if (_deathNotifier != null) _deathNotifier.OnEnemyDied -= HandleKill;
        if (_unlockState != null) _unlockState.OnSoulConvergenceUnlocked -= OnUnlocked;
        ResetMultipliers();
    }

    // ── Kill handler ──────────────────────────────────────────
    void HandleKill()
    {
        if (!IsActive() || _abilityActive || _charged) return;
        _soulCount = Mathf.Min(_soulCount + 1, _soulCap);
        if (_soulCount >= _soulCap) _charged = true;
        RefreshCounter();
    }

    // ── Update ────────────────────────────────────────────────
    void Update()
    {
        TickAbility();
        HandleInput();
    }

    void HandleInput()
    {
        if (!IsActive() || !_charged || _abilityActive) return;
        if (_rescueActive != null && (_rescueActive.IsRescueActive || _rescueActive.HasActiveRescueTarget)) { CancelCharge(); return; }

        if (_input?.GetConvergenceHeld() ?? false)
        {
            if (_chargeProgress == 0f)
            {
                PlayVFX(_leftCharge);
                PlayVFX(_rightCharge);
            }

            _chargeProgress = Mathf.Clamp01(_chargeProgress + Time.deltaTime / _chargeHoldTime);
            if (_chargeBar != null)
            {
                _chargeBar.gameObject.SetActive(true);
                _chargeBar.value = _chargeProgress;
            }
            if (_chargeProgress >= 1f) Activate();
        }
        else CancelCharge();
    }

    void TickAbility()
    {
        if (!_abilityActive) return;
        _powerTimer -= Time.deltaTime;
        if (_powerTimerText) _powerTimerText.text = $"{_powerTimer:F1}s";
        if (_powerTimer <= 0f) Deactivate();
    }

    // ── Activation ────────────────────────────────────────────
    void Activate()
    {
        _abilityActive = true;
        _chargeProgress = 0f;
        _powerTimer = CurrentPowerDuration;
        _soulCount = 0;
        _charged = false;

        DamageOutMultiplier = 1f + _damageOutBonus;
        DamageInMultiplier = 1f - _damageInReduction;
        if (_healthPool) _healthPool.IncomingDamageMultiplier = DamageInMultiplier;

        StopVFX(_leftCharge);
        StopVFX(_rightCharge);
        _leftShieldInstance = SpawnShield(_leftTwin);
        _rightShieldInstance = SpawnShield(_rightTwin);

        _chargeBar?.gameObject.SetActive(false);
        _powerStatePanel?.SetActive(true);
        RefreshCounter();
    }

    void Deactivate()
    {
        _abilityActive = false;
        ResetMultipliers();

        DestroyShield(ref _leftShieldInstance);
        DestroyShield(ref _rightShieldInstance);

        _powerStatePanel?.SetActive(false);
        RefreshCounter();
    }

    void CancelCharge()
    {
        if (_chargeProgress > 0f)
        {
            StopVFX(_leftCharge);
            StopVFX(_rightCharge);
        }
        _chargeProgress = 0f;
        if (_chargeBar) { _chargeBar.value = 0; _chargeBar.gameObject.SetActive(false); }
    }

    void ResetMultipliers()
    {
        DamageOutMultiplier = 1f;
        DamageInMultiplier = 1f;
        if (_healthPool) _healthPool.IncomingDamageMultiplier = 1f;
    }

    // ── UI ────────────────────────────────────────────────────
    void RefreshCounter()
    {
        string display = "";
        if (IsActive() && !_abilityActive)
            display = _charged ? "F" : $"{_soulCount} / {_soulCap}";

        if (_counterText != null) _counterText.text = display;
        if (_accordCounterText != null) _accordCounterText.text = display;
    }

    // ── VFX helpers ───────────────────────────────────────────
    ParticleSystem SpawnChargeVFX(GameObject prefab, Player twin)
    {
        if (prefab == null || twin == null) return null;
        var go = Instantiate(prefab, twin.transform.position,
                             prefab.transform.rotation, twin.transform);
        var ps = go.GetComponent<ParticleSystem>();
        ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    void PlayVFX(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play(true);
    }

    void StopVFX(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    GameObject SpawnShield(Player twin)
    {
        if (_shieldPrefab == null || twin == null) return null;
        var go = Instantiate(_shieldPrefab, twin.transform.position,
                             _shieldPrefab.transform.rotation, twin.transform);
        var anim = go.GetComponent<Animation>();
        if (anim != null) anim.Play();
        return go;
    }

    void DestroyShield(ref GameObject instance)
    {
        if (instance == null) return;
        Destroy(instance);
        instance = null;
    }

    // ── Data ──────────────────────────────────────────────────
    float CurrentPowerDuration
    {
        get
        {
            var data = _dataStore?.SoulConvData;
            if (data == null) return _basePowerDuration;
            float v = _basePowerDuration;
            for (int i = 0; i < data.currentNodeIndex && i < data.nodes.Count; i++)
                v += data.nodes[i].soulDurationBonus;
            return v;
        }
    }

    bool IsActive() => _unlockState != null && _unlockState.IsSoulConvergenceUnlocked;
    void OnUnlocked() => RefreshCounter();
}