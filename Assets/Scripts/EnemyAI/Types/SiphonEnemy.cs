using UnityEngine;

/// <summary>
/// Siphon — ranged kiting enemy that spawns a Ghost during rescue events.
/// Extends RangedEnemy for projectile attack and kite behaviour.
/// Ghost spawn is event-driven via Initialise() and rescue events.
/// </summary>
public class SiphonEnemy : RangedEnemy
{
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
    private SiphonGhost _ghost;
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

        var go = Instantiate(_ghostPrefab, transform.position, Quaternion.identity);
        var ghost = go.GetComponent<SiphonGhost>();
        if (ghost == null)
        {
            Destroy(go);
            _rescueController.UnregisterGhost();
            _ghostSpawned = false;
            return;
        }

        _ghost = ghost;
        ghost.Initialise(_soulPlayer, _rescueController, _siphonData);
        Health.OnDeath += HandleSiphonDeath;
    }

    private void HandleSiphonDeath() => _ghost?.KillOnSiphonDeath();
    private void HandleRescueResolved() => _ghostSpawned = false;

    private void OnDisable()
    {
        Health.OnDeath -= HandleSiphonDeath;
        _ghost = null;
    }

    public void SpawnPanicBomb()
    {
        if (_bombPrefab == null || _panicBombData == null) return;
        if (Target == null) return;

        Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
        Vector3 targetPos = Target.position;
        targetPos.y = spawnPos.y;

        var go = Instantiate(_bombPrefab, spawnPos, Quaternion.identity);
        var bomb = go.GetComponent<BombProjectile>();
        if (bomb == null) return;

        bomb.Roll(spawnPos, targetPos,
            _siphonData?.panicBombTravelDuration ?? 1f,
            _siphonData?.panicBombDetonationDelay ?? 0.75f,
            _siphonData?.panicBombAoeRadius ?? 1.5f,
            _panicBombData, _playerLayer, LayerMask.GetMask("Enemy"));
    }
}