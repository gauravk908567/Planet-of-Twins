using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
// SoulConvergenceSystem
//
// Implements IDamageMultiplier — attack scripts inject this and read
// DamageOutMultiplier each frame. No static property anywhere.
//
// DEPENDENCIES (inject in Inspector):
//   ISkillUnlockState   — am I active?
//   IAbilityDataStore   — upgrade values
//   IEnemyDeathNotifier — drag EnemyDeathNotifier GO here
//   SharedHealthPool    — direct ref, same scene
//
// SOUL COUNTER RULES:
//   Fills +1 per kill → hard cap 20 → hold F 0.75s → 7s power state
//   Counter freezes at 20 until ability fully completes → resets to 0
//   Kills during active power state are ignored
// ─────────────────────────────────────────────────────────────────────────────
public class SoulConvergenceSystem : MonoBehaviour, IDamageMultiplier
{
    // ── IDamageMultiplier — injected into attack scripts ──────────────────────
    public float DamageOutMultiplier { get; private set; } = 1f;
    public float DamageInMultiplier { get; private set; } = 1f;

    [Header("Inject")]
    [SerializeField] private MonoBehaviour _unlockStateMono;      // → ISkillUnlockState
    [SerializeField] private MonoBehaviour _dataStoreMono;        // → IAbilityDataStore
    [SerializeField] private MonoBehaviour _deathNotifierMono;    // → IEnemyDeathNotifier
    [SerializeField] private MonoBehaviour _rescueActiveMono;     // → IRescueActive (RescueEventController)
    [SerializeField] private SharedHealthPool _healthPool;

    private ISkillUnlockState _unlockState;
    private IAbilityDataStore _dataStore;
    private IEnemyDeathNotifier _deathNotifier;
    private IRescueActive _rescueActive;

    [Header("Base Settings")]
    [SerializeField] private int _soulCap = 20;
    [SerializeField] private float _basePowerDuration = 7f;
    [SerializeField] private float _damageOutBonus = 0.35f;
    [SerializeField] private float _damageInReduction = 0.35f;
    [SerializeField] private float _chargeHoldTime = 0.75f;
    [SerializeField] private KeyCode _activateKey = KeyCode.F;

    [Header("HUD UI")]
    [SerializeField] private TMP_Text _counterText;
    [SerializeField] private TMP_Text _chargedText;
    [SerializeField] private Slider _chargeBar;
    [SerializeField] private GameObject _powerStatePanel;
    [SerializeField] private TMP_Text _powerTimerText;

    private int _soulCount = 0;
    private bool _charged = false;
    private bool _abilityActive = false;
    private float _chargeProgress = 0f;
    private float _powerTimer = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _dataStore = _dataStoreMono as IAbilityDataStore;
        _deathNotifier = _deathNotifierMono as IEnemyDeathNotifier;
        _rescueActive = _rescueActiveMono as IRescueActive;

        if (_unlockState == null) Debug.LogError("[SoulConv] Missing ISkillUnlockState");
        if (_dataStore == null) Debug.LogError("[SoulConv] Missing IAbilityDataStore");
        if (_deathNotifier == null) Debug.LogError("[SoulConv] Missing IEnemyDeathNotifier — drag EnemyDeathNotifier GO");

        ResetMultipliers();
    }

    void Start()
    {
        _chargeBar?.gameObject.SetActive(false);
        _powerStatePanel?.SetActive(false);
        RefreshCounter();
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

    // ── Kill handler ──────────────────────────────────────────────────────────
    void HandleKill()
    {
        if (!IsActive() || _abilityActive || _charged) return;

        _soulCount = Mathf.Min(_soulCount + 1, _soulCap);
        if (_soulCount >= _soulCap) _charged = true;
        RefreshCounter();
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        TickAbility();
        HandleInput();
    }

    void HandleInput()
    {
        if (!IsActive() || !_charged || _abilityActive) return;
        if (_rescueActive != null && _rescueActive.IsRescueActive) { CancelCharge(); return; }

        if (Input.GetKey(_activateKey))
        {
            _chargeProgress = Mathf.Clamp01(_chargeProgress + Time.deltaTime / _chargeHoldTime);
            if (_chargeBar != null) { _chargeBar.gameObject.SetActive(true); _chargeBar.value = _chargeProgress; }
            if (_chargeProgress >= 1f) Activate();
        }
        else CancelCharge();
    }

    void TickAbility()
    {
        if (!_abilityActive) return;
        _powerTimer -= Time.deltaTime;
        Debug.Log($"[SoulConv] TickAbility — timer={_powerTimer:F1}, panel={_powerStatePanel?.activeSelf}, timerText={_powerTimerText?.text}");
        if (_powerTimerText) _powerTimerText.text = $"{_powerTimer:F1}s";
        if (_powerTimer <= 0f) Deactivate();
    }

    // ── Activation ────────────────────────────────────────────────────────────
    void Activate()
    {
        _abilityActive = true; _chargeProgress = 0f;
        _powerTimer = CurrentPowerDuration;
        _soulCount = 0; _charged = false;

        DamageOutMultiplier = 1f + _damageOutBonus;
        DamageInMultiplier = 1f - _damageInReduction;
        if (_healthPool) _healthPool.IncomingDamageMultiplier = DamageInMultiplier;

        _chargeBar?.gameObject.SetActive(false);
        _powerStatePanel?.SetActive(true);
        RefreshCounter();
    }

    void Deactivate()
    {
        _abilityActive = false;
        ResetMultipliers();
        _powerStatePanel?.SetActive(false);
        RefreshCounter();
    }

    void CancelCharge()
    {
        _chargeProgress = 0f;
        if (_chargeBar) { _chargeBar.value = 0; _chargeBar.gameObject.SetActive(false); }
    }

    void ResetMultipliers()
    {
        DamageOutMultiplier = 1f;
        DamageInMultiplier = 1f;
        if (_healthPool) _healthPool.IncomingDamageMultiplier = 1f;
    }

    // ── UI ────────────────────────────────────────────────────────────────────
    void RefreshCounter()
    {
        if (_counterText == null) return;
        if (!IsActive()) { _counterText.text = ""; return; }
        if (_abilityActive) { _counterText.text = ""; return; }
        if (_charged) { _counterText.text = $"F"; return; }
        _counterText.text = $"{_soulCount}</b> / {_soulCap}";
    }

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