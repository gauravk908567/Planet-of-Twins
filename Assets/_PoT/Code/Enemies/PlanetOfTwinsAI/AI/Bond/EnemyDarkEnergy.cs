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

    [Header("Corruption State VFX")]
    [Tooltip("Held aura for the 'consumed by corruption' STATE — played once when the bond breaks and held until " +
             "death/despawn. This is a distinct enemy STATE, NOT a mood (it does not go through EnemyMoodSystem): " +
             "the corrupted enemy is independent of its clan. Its own book/id so it is independent of the archetype " +
             "VfxBook and of the Manpu mood layer. Null book/id = no aura (fail-safe — author later).")]
    [SerializeField] private CueBookData _corruptionStateBook;
    [SerializeField] private string _corruptionStateCueId = "poi_corrupt";

    [Header("POI Feed Buff")]
    [Tooltip("Dark-energy threshold for the POI-feed stat buff. First crossing latches it: a small " +
             "damage bump plus a persistent buff aura (played once, held until death/despawn). " +
             "Composes with commander/Witness buffs — it never stomps SetDamageMultiplier.")]
    [SerializeField] private float _poiBuffThreshold = 0.5f;
    [Tooltip("Outgoing damage multiplier once buffed (1.1 = +10%, 'a very small increase').")]
    [SerializeField] private float _poiBuffDamageMultiplier = 1.1f;
    // Held buff aura — always the Common book (CommonFx, zero wiring). Author a "poi_buff" id there;
    // until it exists the stat buff still applies, just without a visual (fail-safe).
    private const string PoiBuffCueId = "poi_buff";

    private float _currentEnergy;
    private bool _bondBroken;
    private bool _comboUnlocked;
    private Enemy _enemy;
    private ZoneEnemyTracker _tracker;

    // held handle for the corruption-state aura (stopped on despawn so it never leaks across pool reuse).
    private CueHandle _corruptionHandle;
    private bool _hasCorruptionAura;

    // POI-feed buff latch + held aura handle (both reset on despawn — pooled reuse starts unbuffed).
    private bool _poiBuffed;
    private CueHandle _poiBuffHandle;

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

    private void OnDisable()   // pool-return / disable — never leak held auras or a stale buff (R8)
    {
        StopCorruptionAura();
        ClearPoiBuff();
        DebugFreezeGain = false;   // bench flag must not survive pool reuse
    }

    private void OnDestroy()
    {
        StopCorruptionAura();
        ClearPoiBuff();
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

    /// <summary>Dev bench (GameDebuggerV2): true = every energy GAIN is ignored so one enemy's level
    /// can be controlled in isolation. Trainer-only; reset on disable (pool reuse).</summary>
    public bool DebugFreezeGain { get; set; }

    public void AddEnergy(float amount)
    {
        if (DebugFreezeGain && amount > 0f) return;
        _currentEnergy = Mathf.Clamp01(_currentEnergy + amount);
    }

    /// <summary>Dev bench (GameDebuggerV2): jump to an absolute energy level — thresholds
    /// (poi_buff / combo / bond-break) fire exactly as a real gain would.</summary>
    public void DebugSetEnergy(float value)
    {
        _currentEnergy = Mathf.Clamp01(value);
        CheckThresholds();
        WriteToBlackboard();
    }

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
        // POI-feed buff — one-way latch at its own (lower) threshold: small damage bump + held aura.
        if (!_poiBuffed && _currentEnergy >= _poiBuffThreshold)
            ApplyPoiBuff();

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

            // The "consumed by corruption" STATE aura (held, owned here — not a mood). The behavioural side stays
 // the Aggressive mood transition below (that is gameplay: energy-burst press)..
            StartCorruptionAura();
            GetComponent<EnemyMoodSystem>()
                ?.TransitionTo(EnemyMood.Aggressive, 5f, EnemyMood.Confident);
        }
    }

    // Bond-break is a one-way latch, so the corruption aura holds from the break until death/despawn.
    private void StartCorruptionAura()
    {
        if (_corruptionStateBook == null || string.IsNullOrEmpty(_corruptionStateCueId)) return;
        var fx = FxManager.Instance;
        if (fx == null) return;
        if (_hasCorruptionAura && fx.IsPlaying(_corruptionHandle)) return;   // already corrupted — don't restack
        _corruptionHandle = fx.PlayBook(_corruptionStateBook, _corruptionStateCueId, CueContext.Follow(transform));
        _hasCorruptionAura = !_corruptionHandle.IsNone;
    }

    private void StopCorruptionAura()
    {
        if (!_hasCorruptionAura) return;
        FxManager.Instance?.Stop(_corruptionHandle);
        _hasCorruptionAura = false;
    }

    // ── POI-feed buff (threshold latch — see the POI Feed Buff header fields) ──
    private void ApplyPoiBuff()
    {
        _poiBuffed = true;

        // Stat side: composes into the attack controller's outgoing damage (its own slot — never
        // stomps the SetDamageMultiplier that Witness/GrandSummoner/ProximityPower share).
        _enemy?.AttackController?.SetPoiBuff(_poiBuffDamageMultiplier);

        // Visual side: the buff aura, played once and held until death/despawn (Common book, no wiring).
        _poiBuffHandle = CommonFx.Play(PoiBuffCueId, CueContext.Follow(transform));

        // Mood tie: a Confident pulse so the Manpu layer announces the power-up the same way moods do.
        GetComponent<EnemyMoodSystem>()?.TransitionTo(EnemyMood.Confident, 4f);

        Debug.Log($"[DarkEnergy] {_enemy?.name} POI BUFF at {_currentEnergy:F2}");
    }

    private void ClearPoiBuff()
    {
        if (!_poiBuffed) return;
        _poiBuffed = false;
        _enemy?.AttackController?.SetPoiBuff(1f);
        FxManager.Instance?.Stop(_poiBuffHandle);
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