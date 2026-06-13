using System.Collections;
using UnityEngine;

/// <summary>
/// Summoner enemy. Extends RangedEnemy so it inherits:
///   - RangedAttackState, RetreatState, ChaseState
///   - SetRangedMode wiring (projectile or raycast)
///   - DesiredRange, MinEngageRange, AttackRange
///
/// Overrides AttackState with SummonerAttackState which handles
/// both ranged shooting AND summoning in priority order.
///
/// Knockback: always blocked — displacing a summoner breaks spawn positioning.
/// </summary>
public class SummonerEnemy : RangedEnemy
{
    public bool IsSummoning { get; private set; } = false;
    public bool CanSummon => !IsSummoning
                               && Time.time >= _nextSummonTime
                               && _activeMinionCount < _maxMinions;

    private float _nextSummonTime = 0f;
    private float _summonCooldown = 10f;
    private float _summonSpawnDelay = 0.6f;
    private int _maxMinions = 3;
    private int _activeMinionCount = 0;
    private SideTypeEntry _summonEntry;
    private EnemySpawner _spawner;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);

        if (data is SummonerEnemyData sd)
        {
            _summonCooldown = sd.summonCooldown;
            _maxMinions = sd.maxMinions;
            _summonEntry = sd.summonEntry;
            _summonSpawnDelay = sd.summonSpawnDelay;
        }
        else
        {
            Debug.LogWarning($"[SummonerEnemy] {name} — expected SummonerEnemyData, " +
                $"got {data?.GetType().Name}. Summon behaviour disabled.", this);
        }
    }

    public void OnMinionDied() => _activeMinionCount = Mathf.Max(0, _activeMinionCount - 1);

    // ── IKnockbackReceiver override ────────────────────────────
    /// <summary>
    /// Summoner is always immune to knockback.
    /// Displacing it during a summon breaks minion spawn positioning and
    /// can cause the summoner to leave its desired range zone.
    /// </summary>
    public override void ReceiveKnockback(KnockbackData data) { }

    // ── Summon API — called by SummonerAttackState ─────────────
    public void TriggerSummon()
    {
        if (!CanSummon) return;
        IsSummoning = true;
        StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        yield return new WaitForSeconds(_summonSpawnDelay);

        _spawner ??= EnemySpawner.Instance;
        if (_spawner != null && _summonEntry != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 1.5f;
            _spawner.SummonerSpawn(_summonEntry, spawnPos);
            FactionEnergySystem.Instance?.OnSummonFired();
            _activeMinionCount++;
        }

        IsSummoning = false;
        _nextSummonTime = Time.time + _summonCooldown;
    }
}