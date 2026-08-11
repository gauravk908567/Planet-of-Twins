using CommonCore;
using UnityEngine;

/// <summary>
/// Tracks faction energy — a shared resource representing how much
/// momentum the enemy faction has. High energy = enemies press harder.
///
/// ATTACH: To AIManager GameObject in scene.
///
/// Sources (energy IN):
///   Enemy near barrier   +0.5/s
///   Ritual active        +3/s
///   Twin takes damage    +2/hit
///   Severed grief state  +5/s
///
/// Drains (energy OUT):
///   Witness buff aura    -0.5/s
///   Summoner summon      -15
///   GroupGrab pile       -2/s
///   TetherBreaker chain  -8
///   Enemy death          -10
///
/// Thresholds:
///   0-25:  Weakened — Cautious mood contagion
///   25-50: Normal
///   50-75: Empowered — Confident mood contagion
///   75-90: Surging — Aggressive mood contagion
///   90+:   EnergyBurst — coordinated rush (GOAPGoalEnergyBurst fires)
/// </summary>
public class FactionEnergySystem : MonoBehaviour
{
    public static FactionEnergySystem Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float _maxEnergy = 100f;
    [SerializeField] private float _startingEnergy = 45f;
    [SerializeField] private float _burstThreshold = 90f;
    [SerializeField] private float _burstDuration = 10f;
    [SerializeField] private float _burstCooldown = 30f;
    [SerializeField] private float _burstDropTarget = 60f;

    private float _currentEnergy;
    private bool _burstActive;
    private float _burstTimer;
    private float _burstCooldownTimer;

    public float CurrentEnergy => _currentEnergy;
    public float NormalisedEnergy => _currentEnergy / _maxEnergy;
    public bool BurstActive => _burstActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentEnergy = _startingEnergy;
    }

    private void Update()
    {
        // Tick burst timers
        if (_burstActive)
        {
            _burstTimer -= Time.deltaTime;
            if (_burstTimer <= 0f)
                EndBurst();
        }
        else if (_burstCooldownTimer > 0f)
        {
            _burstCooldownTimer -= Time.deltaTime;
        }

        // Passive drain — energy naturally decays toward 40 when no activity
        float target = 40f;
        _currentEnergy = Mathf.MoveTowards(_currentEnergy, target, 0.2f * Time.deltaTime);
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, _maxEnergy);

        // Check burst threshold
        if (!_burstActive && _burstCooldownTimer <= 0f && _currentEnergy >= _burstThreshold)
            StartBurst();

        // Write to shared Blackboard
        var shared = BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        shared?.Set(PoTNames.FactionEnergyNorm, NormalisedEnergy);
        shared?.Set(PoTNames.EnergyBurstActive, _burstActive);
    }

    // ── Public API — called by game systems ────────────────
    public void AddEnergy(float amount)
    {
        _currentEnergy = Mathf.Clamp(_currentEnergy + amount, 0f, _maxEnergy);
    }

    public void DrainEnergy(float amount)
    {
        _currentEnergy = Mathf.Clamp(_currentEnergy - amount, 0f, _maxEnergy);
    }

    // ── Convenience methods — called from enemy events ─────
    public void OnTwinTookDamage() => AddEnergy(2f);
    public void OnEnemyDied() => DrainEnergy(10f);
    public void OnSummonFired() => DrainEnergy(15f);
    public void OnChainFired() => DrainEnergy(8f);
    public void OnGrabPileActive() => DrainEnergy(2f * Time.deltaTime);
    public void OnWitnessAuraActive() => DrainEnergy(0.5f * Time.deltaTime);
    public void OnEnemyNearBarrier() => AddEnergy(0.5f * Time.deltaTime);
    public void OnRitualActive() => AddEnergy(3f * Time.deltaTime);
    public void OnSeveredGrieving() => AddEnergy(5f * Time.deltaTime);

    // ── Burst ──────────────────────────────────────────────
    private void StartBurst()
    {
        _burstActive = true;
        _burstTimer = _burstDuration;
        MoodEventBus.EnergyBurst();
        Debug.Log($"[FactionEnergy] BURST started — energy={_currentEnergy:F0}");
    }

    private void EndBurst()
    {
        _burstActive = false;
        _burstCooldownTimer = _burstCooldown;
        _currentEnergy = _burstDropTarget;
        Debug.Log($"[FactionEnergy] Burst ended — energy dropped to {_currentEnergy:F0}");
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Show energy level in scene view
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
            $"Energy: {_currentEnergy:F0}/{_maxEnergy} {(_burstActive ? "BURST" : "")}");
    }
#endif
}