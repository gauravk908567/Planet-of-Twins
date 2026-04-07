using UnityEngine;

/// <summary>
/// Siphon — ranged kiting enemy that spawns a Ghost during rescue events.
///
/// STATES: Idle → KiteState ↔ RetreatState → PossessedState
///
/// GHOST SPAWN: When rescue fires AND soul has arrived AND Siphon is within
/// ghostTriggerRadius of either twin → spawns SiphonGhost via RescueEventController.
///
/// POOL SETUP:
///   Do NOT wire scene references on the prefab asset — they'll be null at spawn.
///   Call Initialise() after pulling from pool. Call Release() before returning.
///   Your pool/spawner holds references to leftTwin, rightTwin, soulPlayer,
///   rescueController and passes them in at spawn time.
///
/// PREFAB SETUP:
///   - Assign SiphonEnemyData SO (this IS safe on the prefab — it's a project asset)
///   - Assign _ghostPrefab, _bombPrefab, _panicBombData (project assets, safe on prefab)
///   - Leave _leftTwin, _rightTwin, _rescueController, _soulPlayer EMPTY on prefab
/// </summary>
public class SiphonEnemy : Enemy
{
    [Header("Siphon — References (project assets — safe to wire on prefab)")]
    [SerializeField] private Transform _bombMuzzle; // child GO at feet — bomb spawns here
    [SerializeField] private GameObject _ghostPrefab;
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private BombEffectData _panicBombData;
    [SerializeField] private LayerMask _playerLayer;

    // Injected at spawn via Initialise() — not serialized, never wire on prefab
    private Player _leftTwin;
    private Player _rightTwin;
    private RescueEventController _rescueController;
    private SoulPlayer _soulPlayer;

    // ── States ─────────────────────────────────────────────────
    public SiphonKiteState KiteState { get; private set; }
    public SiphonRetreatState RetreatState { get; private set; }

    private SiphonEnemyData _siphonData;
    private bool _ghostSpawned;

    protected override void InitStates()
    {
        base.InitStates();

        _siphonData = Data as SiphonEnemyData ?? ScriptableObject.CreateInstance<SiphonEnemyData>();

        KiteState = new SiphonKiteState(this, _siphonData);
        RetreatState = new SiphonRetreatState(this, _siphonData);

        AttackState = KiteState;
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is SiphonEnemyData sd)
        {
            _siphonData = sd;
            KiteState = new SiphonKiteState(this, _siphonData);
            RetreatState = new SiphonRetreatState(this, _siphonData);
            AttackState = KiteState;
        }
    }

    // ── Pool lifecycle ─────────────────────────────────────────

    /// <summary>
    /// Call this immediately after pulling from the pool.
    /// Injects all scene references and subscribes to rescue events.
    /// OnEnable fires BEFORE this — do not subscribe in OnEnable.
    /// </summary>
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



    /// <summary>
    /// Call this before returning to the pool.
    /// Unsubscribes events so a dormant pooled Siphon doesn't react to live rescues.
    /// </summary>
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

    // OnEnable/OnDisable intentionally absent.
    // Pool objects use Initialise()/Release() — OnEnable fires before injection.

    // ── Ghost spawn ────────────────────────────────────────────
    private void HandleSoulArrived()
    {
        if (_ghostSpawned) return;
        if (_ghostPrefab == null) { Debug.LogWarning("[SiphonEnemy] No ghost prefab assigned."); return; }
        if (_soulPlayer == null) { Debug.LogWarning("[SiphonEnemy] SoulPlayer null — was Initialise() called?"); return; }

        // Check trigger radius — only spawn if Siphon is close enough to matter
        if (_leftTwin != null && _rightTwin != null)
        {
            float distLeft = Vector3.Distance(transform.position, _leftTwin.transform.position);
            float distRight = Vector3.Distance(transform.position, _rightTwin.transform.position);
            float triggerRadius = _siphonData?.ghostTriggerRadius ?? 9f;

            if (distLeft > triggerRadius && distRight > triggerRadius)
            {
                Debug.Log("[SiphonEnemy] Too far from both twins — ghost not spawned.");
                return;
            }
        }

        if (!_rescueController.TryRegisterGhost())
        {
            Debug.Log("[SiphonEnemy] TryRegisterGhost() returned false — cap reached.");
            return;
        }

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
            Debug.LogError("[SiphonEnemy] Ghost prefab missing SiphonGhost component.");
            Destroy(go);
            _rescueController.UnregisterGhost();
            _ghostSpawned = false;
            return;
        }

        ghost.Initialise(_soulPlayer, _rescueController, _siphonData);

        // Ghost dies when Siphon dies — no purpose without its owner
        Health.OnDeath += () => ghost?.KillOnSiphonDeath();
    }

    private void HandleRescueResolved()
    {
        _ghostSpawned = false;
    }

    // ── Panic bomb ─────────────────────────────────────────────
    /// <summary>
    /// Spawns bomb at Siphon position and rolls it toward the target's current position.
    /// Called by SiphonRetreatState after wind-up completes.
    /// </summary>
    public void SpawnPanicBomb()
    {
        if (_bombPrefab == null || _panicBombData == null) return;
        if (Target == null) return;

        // Use muzzle point if set — avoids ground raycast issues
        Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
        Vector3 targetPos = Target.position;

        // Keep bomb on ground — match Siphon Y
        targetPos.y = spawnPos.y;

        var go = Instantiate(_bombPrefab, spawnPos, Quaternion.identity);
        var bomb = go.GetComponent<BombProjectile>();
        if (bomb == null) return;

        float travelDuration = _siphonData?.panicBombTravelDuration ?? 1.0f;
        float delay = _siphonData?.panicBombDetonationDelay ?? 0.75f;
        float radius = _siphonData?.panicBombAoeRadius ?? 1.5f;

        bomb.Roll(spawnPos, targetPos, travelDuration, delay, radius,
                  _panicBombData, _playerLayer, LayerMask.GetMask("Enemy"));
    }

    public new EnemyData Data => base.Data;
}