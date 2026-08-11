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
    // ── VFX cue override (EnemyVfxLibrary, R4) — Summoner's own ranged attack ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Summoner;
    protected override string RangedAttackCueId => FxIds.Enemy.Summoner.On_smmAttack;

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
        _spawner = FindAnyObjectByType<EnemySpawner>();
        if (_spawner == null)
            Debug.LogWarning("[SummonerEnemy] No EnemySpawner found in scene.", this);
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
    private CueHandle _summonCueHandle;   // held summon-circle cue — stopped when the summon completes

    public void TriggerSummon()
    {
        if (!CanSummon) return;
        IsSummoning = true;
        // Summon channel cue on the summoner — held for the channel, stopped in SummonRoutine when the
        // summon completes (the circle must not outlive the summon). The minion itself spawns silent:
        // this circle IS its spawn tell (playSpawnCue:false downstream).
        _summonCueHandle = PlayCue(FxIds.Enemy.Summoner.On_smnSummon, CueContext.Follow(transform, owner: this));
        StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        yield return new WaitForSeconds(_summonSpawnDelay);

        if (_summonEntry == null || _summonEntry.prefab == null)
        {
            Debug.LogError($"[SummonerEnemy] {name} — SummonerEnemyData.summonEntry has no prefab; " +
                           "summon spawns nothing. Assign it on the data asset.", this);
        }
        else
        {
            Vector3 spawnPos = transform.position + transform.forward * 1.5f;
            GameObject minion;
            if (_spawner != null)
            {
                // Area path — zone tracking + rescue registration ride along.
                minion = _spawner.SummonerSpawn(_summonEntry, spawnPos);
            }
            else
            {
                // No EnemySpawner in this scene (TestLab / direct-play) — canonical pooled spawn instead.
                var pool = EnemyPool.Instance;
                if (pool == null)
                {
                    Debug.LogError("[SummonerEnemy] No EnemySpawner AND EnemyPool.Instance unresolved — summon skipped.", this);
                    minion = null;
                }
                else
                {
                    minion = pool.SpawnReady(_summonEntry.prefab, spawnPos, Quaternion.identity,
                                             _summonEntry.data, playSpawnCue: false);
                }
            }

            if (minion != null)
            {
                FactionEnergySystem.Instance?.OnSummonFired();
                _activeMinionCount++;
                var minionEnemy = minion.GetComponent<Enemy>();
                if (minionEnemy != null) TrackMinion(minionEnemy);
            }
        }

        // Linger the circle briefly after the summon lands so the player reads WHOSE power the new
        // enemy came from (user call, playtest round 3 — instant stop was too abrupt). Scaled time —
        // the beat freezes with the world; pool Return's StopAllOn stays the death/despawn safety net.
        yield return new WaitForSeconds(0.75f);
        FxManager.Instance?.Stop(_summonCueHandle);
        _summonCueHandle = CueHandle.None;

        IsSummoning = false;
        _nextSummonTime = Time.time + _summonCooldown;
    }

    // Decrement the live-minion count when this minion dies, so CanSummon doesn't saturate at
    // maxMinions forever (OnMinionDied previously had no caller). Self-unsubscribing named handler (R8).
    private void TrackMinion(Enemy minion)
    {
        var health = minion.Health;
        if (health == null) return;
        void HandleMinionDeath()
        {
            health.OnDeath -= HandleMinionDeath;
            OnMinionDied();
        }
        health.OnDeath += HandleMinionDeath;
    }
}