using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grand Summoner — Luminari
/// Commands: 1 Witness + 1 Ranged + 1 Siphon
///
/// Signature — Divine Shaft:
///   Every 3.5s buffs the lowest HP governed soldier.
///   Target gets +attack (×1.5) and +speed (×1.4) for 2.5s, tapering.
///   Projectile visual deferred to animation pass.
///
/// Brain: GOAPBrainGrandSummoner
/// </summary>
public class GrandSummoner : Enemy, ICommander
{
    [Header("Divine Shaft")]
    [SerializeField] private float _interval = 3.5f;
    [SerializeField] private float _buffDuration = 2.5f;
    [SerializeField] private float _speedMult = 1.4f;
    [SerializeField] private float _damageMult = 1.5f;

    [Header("Commander")]
    [SerializeField] private float _commandRadius = 15f;
    [SerializeField] private float _deathRageDuration = 4.5f;

    private readonly List<Enemy> _soldiers = new();
    private float _lastShaft = 0f;
    private bool _dead = false;

    // ── ICommander ─────────────────────────────────────────────
    public bool IsAlive => !_dead && !Health.IsDead;
    public float CommandRadius => _commandRadius;

    public Vector3 GetSlotWorldPosition(Vector3 localOffset)
        => transform.position + transform.TransformDirection(localOffset);

    public void RegisterSoldier(Enemy soldier)
    {
        if (soldier == null || _soldiers.Contains(soldier)) return;
        _soldiers.Add(soldier);
        soldier.Health.OnDeath += () => _soldiers.Remove(soldier);
        soldier.GetComponent<GOAPGoalHoldFormation>()?.SetCommander(this);
    }

    public IReadOnlyList<Enemy> Soldiers => _soldiers;

    // ── Init ───────────────────────────────────────────────────
    public void InitialiseCommander(float commandRadius, float deathRageDuration)
    {
        _commandRadius = commandRadius;
        _deathRageDuration = deathRageDuration;
    }

    protected override void Awake()
    {
        base.Awake();
        Health.OnDeath += OnCommanderDied;
    }

    // ── Divine Shaft ───────────────────────────────────────────
    private void Update()
    {
        if (Health.IsDead) return;
        if (Time.time - _lastShaft < _interval) return;
        if (_soldiers.Count == 0) return;
        FireDivineShaft();
    }

    private void FireDivineShaft()
    {
        Enemy target = null;
        float lowest = float.MaxValue;
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
            float norm = s.Health.CurrentHealth / s.Health.MaxHealth;
            if (norm < lowest) { lowest = norm; target = s; }
        }
        if (target == null) return;

        _lastShaft = Time.time;
        StartCoroutine(ApplyDivineShaft(target));
        Debug.Log($"[GrandSummoner] STUB DivineShaft → {target.name}");
 // TODO: DivineShaft commander-buff VFX retired with EnemyVFXController; re-express the buff via the
        // Common on_AlliesBuff cue (as Witness does) when the commander archetypes are finished.
    }

    private IEnumerator ApplyDivineShaft(Enemy target)
    {
        float baseSpeed = target.Data?.moveSpeed ?? 3.5f;
        target.AttackController.SetDamageMultiplier(_damageMult);
        target.Movement.SetSpeed(baseSpeed * _speedMult);
 // TODO: buffed-target VFX retired with EnemyVFXController — wire the Common on_AlliesBuff cue here
        // (Follow the target) when the commander archetypes are finished.

        float elapsed = 0f;
        while (elapsed < _buffDuration)
        {
            if (target == null || target.Health.IsDead) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / _buffDuration;
            target.AttackController.SetDamageMultiplier(Mathf.Lerp(_damageMult, 1f, t));
            target.Movement.SetSpeed(Mathf.Lerp(baseSpeed * _speedMult, baseSpeed, t));
            yield return null;
        }
        target.AttackController.ClearDamageMultiplier();
        target.Movement.SetSpeed(baseSpeed);
    }

    // ── Death cascade ──────────────────────────────────────────
    private void OnCommanderDied()
    {
        if (_dead) return;
        _dead = true;
        StartCoroutine(DeathCascade());
        MoodEventBus.AllyDied(gameObject);
    }

    private IEnumerator DeathCascade()
    {
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
 // Enraged mood transition drives the soldier's Manpu rage aura autonomously — the parallel
            // EnemyVFXController.PlayRage is retired.
            s.GetComponent<EnemyMoodSystem>()
             ?.TransitionTo(EnemyMood.Enraged, _deathRageDuration, EnemyMood.Aggressive);
        }
        yield return new WaitForSeconds(_deathRageDuration);
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
            s.GetComponent<GOAPGoalHoldFormation>()?.ClearCommander();
        }
        _soldiers.Clear();
    }
}