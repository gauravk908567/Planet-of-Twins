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
/// Big summon cooldown = summoner relies on minions; shoots between summons.
/// </summary>
public class SummonerEnemy : RangedEnemy
{
    // ── Runtime state ─────────────────────────────────────────
    public bool IsSummoning { get; private set; } = false;
    public bool CanSummon => !IsSummoning
                               && UnityEngine.Time.time >= _nextSummonTime
                               && _activeMinionCount < _maxMinions;

    private float _nextSummonTime = 0f;
    private float _summonCooldown = 10f;
    private float _summonSpawnDelay = 0.6f;
    private int _maxMinions = 3;
    private int _activeMinionCount = 0;
    private SideTypeEntry _summonEntry;
    private EnemySpawner _spawner;

    // ── Unity ─────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();

        // Cache spawner once. Previous version called FindAnyObjectByType inside
        // the summon coroutine on every cast — O(n) scene scan per summon.
        _spawner = FindAnyObjectByType<EnemySpawner>();
        if (_spawner == null)
            UnityEngine.Debug.LogWarning("[SummonerEnemy] No EnemySpawner found in scene.", this);
    }

    // ── States ────────────────────────────────────────────────
    protected override void InitStates()
    {
        base.InitStates(); // sets IdleState, ChaseState, AttackState(Ranged), RetreatState, PossessedState

        // Replace RangedAttackState with the combined summon+ranged state
        AttackState = new SummonerAttackState(this);
    }

    // ── Data ──────────────────────────────────────────────────
    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data); // handles base + RangedEnemyData fields

        if (data is SummonerEnemyData sd)
        {
            _summonCooldown = sd.summonCooldown;
            _maxMinions = sd.maxMinions;
            _summonEntry = sd.summonEntry;
            _summonSpawnDelay = sd.summonSpawnDelay;
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[SummonerEnemy] {name} — expected SummonerEnemyData, " +
                $"got {data?.GetType().Name}. Summon behaviour disabled.", this);
        }
    }

    // ── Summon API — called by SummonerAttackState ────────────
    public void TriggerSummon()
    {
        if (!CanSummon) return;
        IsSummoning = true;
        StartCoroutine(SummonRoutine());
    }

    private System.Collections.IEnumerator SummonRoutine()
    {
        yield return new UnityEngine.WaitForSeconds(_summonSpawnDelay);

        if (_spawner != null && _summonEntry != null)
        {
            UnityEngine.Vector3 spawnPos =
                transform.position + transform.forward * 1.5f;
            _spawner.SummonerSpawn(_summonEntry, spawnPos);
            _activeMinionCount++;
        }

        IsSummoning = false;
        _nextSummonTime = UnityEngine.Time.time + _summonCooldown;
    }
}