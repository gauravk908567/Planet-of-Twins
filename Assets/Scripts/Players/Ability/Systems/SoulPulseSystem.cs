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

    [Header("Pulse — override by AbilityUpgradeData at runtime")]
    [SerializeField] private float _pulseInterval = 3.5f;
    [SerializeField] private float _pulseRadius = 4f;
    [SerializeField] private float _fearDuration = 1.5f;
    [SerializeField] private float _slowMultiplier = 0.6f; // 0.6 = -40% speed
    [SerializeField] private float _slowDuration = 1.5f;

    [Header("Ashen Tide (Accord mode only)")]
    [SerializeField] private float _burnDps = 0.35f;
    [SerializeField] private float _burnDuration = 1.8f; // per pulse hit, stacks

    // ── Runtime ───────────────────────────────────────────────
    private IAccordModeProvider _accordMode;
    private Coroutine _pulseCoroutine;

    // Track burn timers per enemy so stacking works correctly
    private readonly Dictionary<EnemyHealthComponent, float> _burnTimers = new();
    private Coroutine _burnTickCoroutine;

    private void Awake()
    {
        _accordMode = _accordModeMono as IAccordModeProvider;
    }

    // ── Called by TeleportAbility ─────────────────────────────
    /// <summary>Start auto-pulsing. Called when soul arrives at destination.</summary>
    public void StartPulsing()
    {
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
        Collider[] hits = Physics.OverlapSphere(
            transform.position, _pulseRadius, _enemyLayer);

        bool accordActive = _accordMode != null && _accordMode.IsAccordActive;

        foreach (Collider hit in hits)
        {
            // Skip the enemy that caused the rescue (IRescueTarget check)
            if (IsRescueAttacker(hit.gameObject)) continue;

            // Fear
            hit.GetComponent<IFearReceiver>()
               ?.ApplyFear(transform.position, _fearDuration);

            // Slow
            hit.GetComponent<ISlowReceiver>()
               ?.ApplySlow(_slowMultiplier, _slowDuration, "soul_pulse");

            // Ashen Tide burn (Accord mode only)
            if (accordActive)
            {
                var health = hit.GetComponent<EnemyHealthComponent>();
                if (health != null)
                {
                    // Stack burn duration — each pulse hit adds _burnDuration
                    if (_burnTimers.ContainsKey(health))
                        _burnTimers[health] += _burnDuration;
                    else
                        _burnTimers[health] = _burnDuration;
                }
            }
        }
    }

    // ── Burn tick loop ────────────────────────────────────────
    private IEnumerator BurnTickLoop()
    {
        const float tickInterval = 0.1f;

        while (true)
        {
            yield return new WaitForSeconds(tickInterval);

            var toRemove = new List<EnemyHealthComponent>();

            foreach (var kvp in _burnTimers)
            {
                var health = kvp.Key;
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
        // The rescue attacker is the IRescueTarget on the enemy GO
        var rescueTarget = enemyGO.GetComponent<IRescueTarget>();
        return rescueTarget != null && rescueTarget == activeTarget;
    }
}