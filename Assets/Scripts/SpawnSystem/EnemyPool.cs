using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : MonoBehaviour, IEnemyPoolProvider
{
    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        [Tooltip("How many instances to pre-create at scene load")]
        public int preWarmCount = 5;
    }

    [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();
    [SerializeField] private Transform poolParent;  // keep hierarchy clean

    private Transform _leftTwin;
    private Transform _rightTwin;

    // Extended scene refs � used to inject into enemies that need them (e.g. SiphonEnemy)
    private Player _leftPlayer;
    private Player _rightPlayer;
    private SoulPlayer _soulPlayer;
    private RescueEventController _rescueController;

    private readonly Dictionary<GameObject, Queue<GameObject>> _pools
        = new Dictionary<GameObject, Queue<GameObject>>();

    /// <summary>Persistent singleton (R4 target for area-resident consumers — e.g. the P12 GameDebugger in
    /// TestLab). Standard pair: duplicate-destroy guard in Awake, nulled in OnDestroy (Restart safety).
    /// Same Phase 5.1 treatment EnemySpawner got; in-scene consumers keep their serialized refs (R1).</summary>
    public static EnemyPool Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (poolParent == null)
            poolParent = transform;

        foreach (var config in poolConfigs)
            PreWarm(config.prefab, config.preWarmCount);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void PreWarm(GameObject prefab, int count)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        for (int i = 0; i < count; i++)
        {
            var instance = CreateInstance(prefab);
            _pools[prefab].Enqueue(instance);
        }
    }

    public void SetTwinReferences(Transform left, Transform right)
    {
        _leftTwin = left;
        _rightTwin = right;
    }

    /// <summary>
    /// Call after SetTwinReferences if SiphonEnemy is in the pool.
    /// Provides the additional scene refs Siphon needs for ghost spawn logic.
    /// </summary>
    public void SetSiphonReferences(Player left, Player right,
                                     SoulPlayer soul, RescueEventController rescue)
    {
        _leftPlayer = left;
        _rightPlayer = right;
        _soulPlayer = soul;
        _rescueController = rescue;
    }

    // IEnemyPoolProvider
    public GameObject Get(GameObject prefab)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        GameObject instance = _pools[prefab].Count > 0
            ? _pools[prefab].Dequeue()
            : CreateInstance(prefab);

        // FIX: disable NavMeshAgent before activating so it doesn't
        // warp to nearest NavMesh on Enable before position is set
        var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        instance.SetActive(true);

        // Reuse starts ALIVE (BUG-058: health was Awake-only, so a reused corpse spawned with 0 HP
        // and was unkillable). At ISSUE time — never during the death event (see Enemy.ResetForPool).
        instance.GetComponent<EnemyHealthComponent>()?.ResetToFull();

        // Inject scene refs into enemies that need them.
        // SiphonEnemy can't hold scene refs on the prefab asset � pool injects at spawn.
        var siphon = instance.GetComponent<SiphonEnemy>();
        if (siphon != null && _rescueController != null)
            siphon.Initialise(_leftPlayer, _rightPlayer, _soulPlayer, _rescueController);

        // Position is set by EnemySpawner AFTER this returns
        // Agent re-enabled in EnemySpawner after position is set
        return instance;
    }

    /// <summary>
    /// The CANONICAL ready-to-fight spawn (P16): Get → position → NavMeshAgent.Warp+enable →
    /// SetPoolProvider (fires on_enemyspawn + reveal-delay) → ITimeAffected register → optional
    /// ApplyData → EnemyDeathNotifier.Register. The same sequence EnemySpawner.SpawnEnemy and
    /// GameDebuggerV2.Spawn run — use THIS for any ad-hoc enemy spawn (e.g. the Witness summon)
    /// so pooled minions get the full lifecycle. (Dedup of the two older copies = a later
 /// isolated commit, .)
    /// </summary>
    public GameObject SpawnReady(GameObject prefab, Vector3 pos, Quaternion rot, EnemyData data = null,
                                 bool playSpawnCue = true)
    {
        if (prefab == null) return null;

        var instance = Get(prefab);
        if (instance == null) return null;

        instance.transform.SetPositionAndRotation(pos, rot);

        var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) { agent.Warp(pos); agent.enabled = true; }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogError($"[EnemyPool] SpawnReady('{prefab.name}') — prefab has no Enemy component.", prefab);
            Return(prefab, instance);
            return null;
        }

        enemy.SetPoolProvider(this, prefab, playSpawnCue);                 // on_enemyspawn + reveal-delay (skipped for summons)
        if (enemy is ITimeAffected ta)
        {
            _timeFactor ??= FindAnyObjectByType<TimeFactorBootstrapper>(); // allowed non-singleton sweep (R4 note)
            _timeFactor?.RegisterEntity(ta);
        }
        if (data != null) enemy.ApplyData(data);

        instance.GetComponent<ZoneEnemyTracker>()?.ResetForPool();         // HomeZone stays null off-zone
        EnemyDeathNotifier.Instance?.Register(enemy.Health);               // kill cues + counters stay real
        return instance;
    }

    private TimeFactorBootstrapper _timeFactor;   // cached on first SpawnReady

    public void Return(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;

        // One throwing step must never abort the return halfway (BUG-056 class — a half-returned
        // instance stays active with stale state). Log the offender, keep resetting.
        try
        {
            // Release injected refs before generic reset
            instance.GetComponent<SiphonEnemy>()?.Release();
            instance.GetComponent<SeveredEnemy>()?.Release();
            instance.GetComponent<TetherBreakerEnemy>()?.Release();
        }
        catch (System.Exception ex) { Debug.LogException(ex, instance); }

        // F2 — stop + reclaim any pooled cue (stun aura, etc.) following this enemy, so it
        // re-enters the pool visually and audibly naked ( F2 / Tier 1).
        try { FxManager.Instance?.StopAllOn(instance.transform); }
        catch (System.Exception ex) { Debug.LogException(ex, instance); }

        // Manpu — wipe the glyph slot so nothing leaks onto the next reused enemy.
        try { instance.GetComponentInChildren<ManpuSlot>(true)?.Clear(); }
        catch (System.Exception ex) { Debug.LogException(ex, instance); }

        // Reset enemy state before returning to pool
        try { instance.GetComponent<Enemy>()?.ResetForPool(); }
        catch (System.Exception ex) { Debug.LogException(ex, instance); }

        instance.SetActive(false);
        instance.transform.SetParent(poolParent);

        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        _pools[prefab].Enqueue(instance);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        var instance = Instantiate(prefab, poolParent);
        instance.SetActive(false);
        return instance;
    }
}