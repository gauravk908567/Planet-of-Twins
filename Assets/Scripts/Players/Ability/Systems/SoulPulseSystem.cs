using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SoulPulseSystem — component on the SoulPlayer GO.
///
/// AUTO PULSE: fires every _pulseInterval seconds while soul is active.
/// Each pulse fears + slows all enemies in range except the rescue attacker.
///
/// ASHEN TIDE (Accord mode): pulse also applies a burning DoT.
/// Burn duration stacks — each pulse adds _burnDuration to the existing timer.
///
/// SETUP:
///   Add to SoulPlayer prefab.
///   Wire: _accordMode, _rescueController, _enemyLayer.
///   Values driven by GateData from AbilityUpgradeData SO.
///
/// ACTIVATION: call StartPulsing() on soul arrival, StopPulsing() on soul return.
/// TeleportAbility calls both via OnSoulArrived and soul return path.
/// </summary>
public class SoulPulseSystem : MonoBehaviour
{
    [Header("Inject")]
    [Tooltip("Drag AccordStateSystem here.")]
    [SerializeField] private MonoBehaviour _accordModeMono;
    [Tooltip("Drag RescueEventController here.")]
    [SerializeField] private RescueEventController _rescueController;

    [Header("Layer")]
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Upgrade Gate")]
    [Tooltip("Drag Gate AbilityUpgradeData SO here — pulse only active at Node 3+")]
    [SerializeField] private AbilityUpgradeData _gateData;
    [Tooltip("Minimum Gate node index required to unlock pulse. Default 3 = Node 3.")]
    [SerializeField] private int _requiredNodeIndex = 3;

    [Header("VFX")]
    [Tooltip("Particle prefab spawned at soul position on each pulse — drag KnockbackParticle here.")]
    [SerializeField] private GameObject _pulseVFXPrefab;

    [Header("Pulse — override by AbilityUpgradeData at runtime")]
    [SerializeField] private float _pulseInterval = 3.5f;
    [SerializeField] private float _pulseRadius = 4f;
    [SerializeField] private float _fearDuration = 1.5f;
    [SerializeField] private float _slowMultiplier = 0.6f; // 0.6 = -40% speed
    [SerializeField] private float _slowDuration = 1.5f;

    [Header("Ashen Tide (Accord mode only)")]
    [SerializeField] private float _burnDps = 3.5f;  // 0.35 per 0.1s tick = visible damage
    [SerializeField] private float _burnDuration = 3.0f; // per pulse hit, stacks — designer tunable

    // ── Runtime ───────────────────────────────────────────────
    private GameObject _lastBlowEnemy; // set by RescueEventController via SetLastBlowEnemy()
    private IAccordModeProvider _accordMode;
    private Coroutine _pulseCoroutine;

    // Track burn timers per enemy so stacking works correctly
    private readonly Dictionary<EnemyHealthComponent, float> _burnTimers = new();
    private Coroutine _burnTickCoroutine;

    private void Awake()
    {
        _accordMode = _accordModeMono as IAccordModeProvider;
    }

    // ── Called by TwinAbilitySetup ────────────────────────────
    public void SetGateData(AbilityUpgradeData data) => _gateData = data;

    // ── Called by RescueEventController ──────────────────────
    /// <summary>Set the enemy that caused the rescue — excluded from fear/burn.</summary>
    public void SetLastBlowEnemy(GameObject enemy) => _lastBlowEnemy = enemy;
    public void ClearLastBlowEnemy() => _lastBlowEnemy = null;

    // ── Called by TeleportAbility ─────────────────────────────
    /// <summary>Start auto-pulsing. Called when soul arrives at destination.</summary>
    public void StartPulsing()
    {
        // Gate behind Gate Node 3 — pulse not available at lower upgrade levels
        if (_gateData != null && _gateData.currentNodeIndex < _requiredNodeIndex)
        {
            Debug.Log($"[SoulPulse] Pulse locked — Gate node {_gateData.currentNodeIndex} < required {_requiredNodeIndex}");
            return;
        }

        StopPulsing();
        _pulseCoroutine = StartCoroutine(PulseLoop());
        _burnTickCoroutine = StartCoroutine(BurnTickLoop());
    }

    /// <summary>Stop all pulsing and clear burn timers. Called when soul returns.</summary>
    public void StopPulsing()
    {
        if (_pulseCoroutine != null) { StopCoroutine(_pulseCoroutine); _pulseCoroutine = null; }
        if (_burnTickCoroutine != null) { StopCoroutine(_burnTickCoroutine); _burnTickCoroutine = null; }
        _burnTimers.Clear();
    }

    // ── Runtime upgrade injection ─────────────────────────────
    /// <summary>
    /// Called by TeleportAbility after soul arrival to apply current Gate upgrade values.
    /// </summary>
    public void ApplyUpgradeValues(
        float pulseInterval, float pulseRadius,
        float fearDuration, float slowMultiplier, float slowDuration,
        float burnDps, float burnDuration)
    {
        _pulseInterval = pulseInterval;
        _pulseRadius = pulseRadius;
        _fearDuration = fearDuration;
        _slowMultiplier = slowMultiplier;
        _slowDuration = slowDuration;
        _burnDps = burnDps;
        _burnDuration = burnDuration;
    }

    // ── Pulse loop ────────────────────────────────────────────
    private IEnumerator PulseLoop()
    {
        // Fire first pulse immediately on arrival
        FirePulse();

        while (true)
        {
            yield return new WaitForSeconds(_pulseInterval);
            FirePulse();
        }
    }

    private void FirePulse()
    {
        // Spawn pulse VFX at soul position
        if (_pulseVFXPrefab != null)
        {
            var vfx = Instantiate(_pulseVFXPrefab, transform.position, Quaternion.identity);
            var ps = vfx.GetComponent<ParticleSystem>();
            Destroy(vfx, ps != null ? ps.main.duration + 0.1f : 1.6f);
        }

        // No-mask overlap then filter by enemy component — handles child colliders
        Collider[] hits = Physics.OverlapSphere(transform.position, _pulseRadius);
        bool accordActive = _accordMode != null && _accordMode.IsAccordActive;
        int enemiesHit = 0;

        foreach (Collider hit in hits)
        {
            var enemy = hit.GetComponent<Enemy>() ?? hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if ((_enemyLayer.value & (1 << enemy.gameObject.layer)) == 0) continue;
            if (IsRescueAttacker(enemy.gameObject)) continue;

            enemiesHit++;

            // Fear
            (enemy as IFearReceiver)?.ApplyFear(transform.position, _fearDuration);

            // Slow
            (enemy as ISlowReceiver)?.ApplySlow(_slowMultiplier, _slowDuration, "soul_pulse");

            // Ashen Tide burn (Accord mode only)
            if (accordActive)
            {
                var health = enemy.GetComponent<EnemyHealthComponent>();
                if (health != null)
                {
                    if (_burnTimers.ContainsKey(health))
                        _burnTimers[health] += _burnDuration;
                    else
                        _burnTimers[health] = _burnDuration;
                }
            }
        }

        Debug.Log($"[SoulPulse] Fired — enemies affected={enemiesHit} accord={accordActive}");
    }

    // ── Burn tick loop ────────────────────────────────────────
    private IEnumerator BurnTickLoop()
    {
        const float tickInterval = 0.1f;

        while (true)
        {
            yield return new WaitForSeconds(tickInterval);

            // Snapshot keys to avoid modifying dictionary during enumeration
            var keys = new List<EnemyHealthComponent>(_burnTimers.Keys);
            var toRemove = new List<EnemyHealthComponent>();

            foreach (var health in keys)
            {
                if (health == null || !health.gameObject.activeInHierarchy)
                {
                    toRemove.Add(health);
                    continue;
                }

                // Apply burn tick
                health.TakeDamage(new DamageData(_burnDps * tickInterval, DamageType.Ability));

                // Decrement timer
                _burnTimers[health] -= tickInterval;
                if (_burnTimers[health] <= 0f)
                    toRemove.Add(health);
            }

            foreach (var key in toRemove)
                _burnTimers.Remove(key);
        }
    }

    // ── Helpers ───────────────────────────────────────────────
    private bool IsRescueAttacker(GameObject enemyGO)
    {
        if (_rescueController == null) return false;
        var activeTarget = _rescueController.ActiveTarget;
        if (activeTarget == null) return false;

        // Compare by MonoBehaviour GameObject — interface reference equality
        // can fail if the activeTarget was set before the component fully initialised
        var rescueTargetMono = activeTarget as MonoBehaviour;
        if (rescueTargetMono == null) return false;

        // Check if the enemy GO or any parent is the rescue attacker's GO
        var check = enemyGO.transform;
        while (check != null)
        {
            if (check.gameObject == rescueTargetMono.gameObject) return true;
            check = check.parent;
        }
        return false;
    }
}