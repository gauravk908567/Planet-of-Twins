using UnityEngine;

/// <summary>
/// Siphon — ranged kiting enemy that spawns a Ghost during rescue events.
/// Extends RangedEnemy for projectile attack and kite behaviour.
/// Ghost spawn is event-driven via Initialise() and rescue events.
/// </summary>
public class SiphonEnemy : RangedEnemy
{
    // ── VFX cue override (EnemyVfxLibrary, R4) — Siphon's own ranged attack ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Siphon;
    protected override string RangedAttackCueId => FxIds.Enemy.Siphon.On_SiphonRangedAttack;

    [Header("Siphon — project assets")]
    [SerializeField] private Transform _bombMuzzle;
    [SerializeField] private GameObject _ghostPrefab;
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private BombEffectData _panicBombData;
    [SerializeField] private LayerMask _playerLayer;

    // Injected at spawn
    private Player _leftTwin;
    private Player _rightTwin;
    private RescueEventController _rescueController;
    private SoulPlayer _soulPlayer;

    private SiphonEnemyData _siphonData;
    private bool _ghostSpawned;

    /// <summary>Debug only — simulates soul arrived event for ghost spawn testing.</summary>
    public void TestTriggerGhostSpawn() => HandleSoulArrived();
    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data); // calls RangedEnemy.ApplyData → SetRangedMode
        if (data is SiphonEnemyData sd)
            _siphonData = sd;
        else
            Debug.LogWarning($"[SiphonEnemy] Expected SiphonEnemyData, got {data?.GetType().Name}", this);
    }

    public void Initialise(Player left, Player right, SoulPlayer soul,
                           RescueEventController rescue)
    {
        _leftTwin = left;
        _rightTwin = right;
        _soulPlayer = soul;
        _rescueController = rescue;
        _ghostSpawned = false;

        _rescueController.OnSoulArrived += HandleSoulArrived;
        _rescueController.OnRescueResolved += HandleRescueResolved;
    }

    public void Release()
    {
        if (_rescueController != null)
        {
            _rescueController.OnSoulArrived -= HandleSoulArrived;
            _rescueController.OnRescueResolved -= HandleRescueResolved;
        }

        _ghostSpawned = false;
        _leftTwin = null;
        _rightTwin = null;
        _soulPlayer = null;
        _rescueController = null;
    }

    private void HandleSoulArrived()
    {
        if (_ghostSpawned) return;
        if (_ghostPrefab == null) return;
        if (_soulPlayer == null) return;

        if (_leftTwin != null && _rightTwin != null)
        {
            float distLeft = Vector3.Distance(transform.position, _leftTwin.transform.position);
            float distRight = Vector3.Distance(transform.position, _rightTwin.transform.position);
            float triggerRadius = _siphonData?.ghostTriggerRadius ?? 9f;
            if (distLeft > triggerRadius && distRight > triggerRadius) return;
        }

        if (!_rescueController.TryRegisterGhost()) return;

        _ghostSpawned = true;
        SpawnGhost();
    }

    private void SpawnGhost()
    {
        if (_soulPlayer == null || _ghostPrefab == null) return;

        RegisterPooledPrefab(_ghostPrefab, PoolCategory.Summons, 1);   // refcounted warm pool, trimmed after death
        var go = GameplayPool.Spawn(_ghostPrefab, PoolCategory.Summons, transform.position, Quaternion.identity);
        var ghost = go != null ? go.GetComponent<SiphonGhost>() : null;
        if (ghost == null)
        {
            GameplayPool.Despawn(go);
            _rescueController.UnregisterGhost();
            _ghostSpawned = false;
            return;
        }

        ghost.Initialise(_soulPlayer, _rescueController, _siphonData);
        // Ghost emergence cue (Siphon book) at the spawn point — the ghost is NOT a pooled Enemy, so it never
        // routes through on_enemyspawn; this is its dedicated spawn tell.
        PlayCue(FxIds.Enemy.Siphon.On_siphonGhostSpawn, new CueContext(go.transform.position));
        // NAMED handler on OUR death (P16): the old lambda captured the ghost forever — with pooling, a stale
        // subscription would kill a REUSED ghost in someone else's rescue. Unsubscribed in HandleRescueResolved.
        _spawnedGhost = ghost;
        Health.OnDeath -= KillSpawnedGhost;
        Health.OnDeath += KillSpawnedGhost;
    }

    private SiphonGhost _spawnedGhost;
    private void KillSpawnedGhost() => _spawnedGhost?.KillOnSiphonDeath();

    private void HandleRescueResolved()
    {
        _ghostSpawned = false;
        Health.OnDeath -= KillSpawnedGhost;   // this rescue's ghost is gone — never touch a pooled reuse
        _spawnedGhost = null;
    }

    public void SpawnPanicBomb()
    {
        // Fail loud on broken authoring (BUG-053 — a rebuilt bomb prefab silently nulled this fileID slot).
        if (_bombPrefab == null || _panicBombData == null)
        {
            Debug.LogError("[Siphon] _bombPrefab/_panicBombData unassigned — panic bomb impossible.", this);
            return;
        }
        if (Target == null) return;

        Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
        Vector3 targetPos = Target.position;
        targetPos.y = spawnPos.y;

        RegisterPooledPrefab(_bombPrefab, PoolCategory.Projectiles);   // refcounted warm pool, trimmed after death
        var go = GameplayPool.Spawn(_bombPrefab, PoolCategory.Projectiles, spawnPos, Quaternion.identity);
        var bomb = go != null ? go.GetComponent<BombProjectile>() : null;
        if (bomb == null) return;

        // Hand the Siphon's own book + bomb ids to the projectile (fuse rides the bomb, explode is world).
        bomb.ConfigureCues(VfxLibraryProvider.Instance?.Enemy?.SiphonBomb,
            FxIds.Enemy.SiphonBomb.On_SiphonBombFuseOn, FxIds.Enemy.SiphonBomb.On_SiphonBombExplode);

        bomb.Roll(spawnPos, targetPos,
            _siphonData?.panicBombTravelDuration ?? 1f,
            _siphonData?.panicBombDetonationDelay ?? 0.75f,
            _siphonData?.panicBombAoeRadius ?? 1.5f,
            _panicBombData, _playerLayer, LayerMask.GetMask("Enemy"));
    }
}