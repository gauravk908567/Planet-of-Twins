using UnityEngine;

/// <summary>
/// Per-enemy dark energy system.
/// Registers with ComboReadyRegistry when ComboUnlocked threshold crossed.
/// </summary>
public class EnemyDarkEnergy : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float _baseEnergy = 0.1f;
    [SerializeField] private float _bondBreakThreshold = 0.8f;
    [SerializeField] private float _comboThreshold = 0.6f;

    [Header("Pact")]
    [Tooltip("If true this enemy never registers with ComboReadyRegistry.\n" +
             "Use on commander prefabs — commanders should not form pacts.")]
    [SerializeField] private bool _excludeFromPact = false;

    [Header("Clan Identity")]
    public ClanAlignment Alignment;
    public AlliedClanType ClanType;

    [Header("Passive Gain Rates")]
    [SerializeField] private float _passiveGainRate = 0.0005f;
    [SerializeField] private float _combatGainPerHit = 0.008f;
    [SerializeField] private float _barrierProximityRate = 0.001f;
    [SerializeField] private float _poiGainRate = 0.002f;
    [SerializeField] private float _alliedBondGainRate = 0.003f;

    private float _currentEnergy;
    private bool _bondBroken;
    private bool _comboUnlocked;
    private Enemy _enemy;
    private ZoneEnemyTracker _tracker;

    public float CurrentEnergy => _currentEnergy;
    public float NormalisedEnergy => _currentEnergy;
    public bool BondBroken => _bondBroken;
    public bool ComboUnlocked => _comboUnlocked;
    public float BondBreakThreshold => _bondBreakThreshold;
    public float ComboThreshold => _comboThreshold;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _tracker = GetComponent<ZoneEnemyTracker>();
        _currentEnergy = _baseEnergy;
    }

    private void OnDestroy()
    {
        ComboReadyRegistry.Instance?.Unregister(this);
    }

    private void Update()
    {
        if (_enemy == null || _enemy.Health.IsDead) return;

        float zoneMultiplier = _tracker?.HomeZone?.areaConfig?.darkEnergyGainMultiplier ?? 1f;
        AddEnergy(_passiveGainRate * zoneMultiplier * Time.deltaTime);

        CheckThresholds();
        WriteToBlackboard();
    }

    public void OnTookHit() => AddEnergy(_combatGainPerHit);
    public void OnNearBarrier() => AddEnergy(_barrierProximityRate * Time.deltaTime);
    public void OnAtPOI() => AddEnergy(_poiGainRate * Time.deltaTime);
    public void OnNearAlliedClan() => AddEnergy(_alliedBondGainRate * Time.deltaTime);

    public void AddEnergy(float amount)
        => _currentEnergy = Mathf.Clamp01(_currentEnergy + amount);

    public void ApplyLevelScaling(float baseEnergyForLevel, float thresholdForLevel)
    {
        _baseEnergy = baseEnergyForLevel;
        _bondBreakThreshold = thresholdForLevel;
        _currentEnergy = Mathf.Max(_currentEnergy, baseEnergyForLevel);
        Debug.Log($"[DarkEnergy] {_enemy?.name} scaled → " +
                  $"base={baseEnergyForLevel:F2} threshold={thresholdForLevel:F2}");
    }

    private void CheckThresholds()
    {
        if (!_comboUnlocked && _currentEnergy >= _comboThreshold)
        {
            _comboUnlocked = true;
            Debug.Log($"[DarkEnergy] {_enemy?.name} COMBO UNLOCKED at {_currentEnergy:F2}");

            // Commanders excluded from pact formation
            if (!_excludeFromPact)
                ComboReadyRegistry.Instance?.Register(this);
        }

        if (!_bondBroken && _currentEnergy >= _bondBreakThreshold)
        {
            _bondBroken = true;
            Debug.Log($"[DarkEnergy] {_enemy?.name} BOND BROKEN at {_currentEnergy:F2}");

            GetComponentInChildren<EnemyVFXController>()?.PlayDarkEnergy();
            GetComponent<EnemyMoodSystem>()
                ?.TransitionTo(EnemyMood.Aggressive, 5f, EnemyMood.Confident);
        }
    }

    private void WriteToBlackboard()
    {
        var brain = GetComponent<PoTGOAPBrainBase>();
        if (brain?.LinkedBlackboard == null) return;
        brain.LinkedBlackboard.Set(PoTNames.EnemyDarkEnergyNorm, _currentEnergy);
        brain.LinkedBlackboard.Set(PoTNames.BondBroken, _bondBroken);
        brain.LinkedBlackboard.Set(PoTNames.ComboReady, _comboUnlocked);
    }
}