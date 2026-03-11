using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Zones — register all SpawnZones in the level here")]
    [SerializeField] private SpawnZone[] allZones;

    [Header("Spawn Timing")]
    [SerializeField] private float spawnInterval = 3f;

    [Header("References")]
    [SerializeField] private MonoBehaviour poolProviderObject;
    [SerializeField] private TimeFactorBootstrapper timeFactorBootstrapper;
    [SerializeField] private Transform barrierTransform;
    [SerializeField] private EnemyDeathNotifier deathNotifier;
    [SerializeField] private MonoBehaviour rescueControllerObject; // → IRescueTrapRegistry

    // ── Runtime ────────────────────────────────────────────────
    private IEnemyPoolProvider _pool;
    private IRescueTrapRegistry _rescueRegistry;

    private SpawnZone _activeZone;
    private AreaZoneConfig _activeConfig;
    private Transform[] _activeLeftPoints;
    private Transform[] _activeRightPoints;

    // Fisher-Yates shuffled queues
    private Transform[] _shuffledLeft;
    private Transform[] _shuffledRight;
    private int _leftIdx;
    private int _rightIdx;

    // Active counts
    private int _activeLeft;
    private int _activeRight;
    private readonly Dictionary<SideTypeEntry, int> _activeCountsLeft = new();
    private readonly Dictionary<SideTypeEntry, int> _activeCountsRight = new();
    private readonly HashSet<GameObject> _allActive = new();

    // ── Unity lifecycle ────────────────────────────────────────
    private void Awake()
    {
        _pool = poolProviderObject as IEnemyPoolProvider;
        _rescueRegistry = rescueControllerObject as IRescueTrapRegistry;

        foreach (var zone in allZones)
        {
            if (zone == null) continue;
            zone.OnZoneEntered += HandleZoneEntered;
            zone.OnZoneExited += HandleZoneExited;
        }
    }

    private void OnDestroy()
    {
        foreach (var zone in allZones)
        {
            if (zone == null) continue;
            zone.OnZoneEntered -= HandleZoneEntered;
            zone.OnZoneExited -= HandleZoneExited;
        }
    }

    // NOTE: No Start() spawn — spawning only begins when zone is entered.

    // ── Zone events ────────────────────────────────────────────
    private Coroutine _zoneExitCoroutine;

    private void HandleZoneEntered(SpawnZone zone)
    {
        if (zone == _activeZone) return;

        if (_zoneExitCoroutine != null)
        {
            StopCoroutine(_zoneExitCoroutine);
            _zoneExitCoroutine = null;
        }

        //Debug.Log($"[EnemySpawner] Zone entered: {zone.areaConfig?.areaName}");
        ActivateZone(zone);
    }

    private void HandleZoneExited(SpawnZone zone)
    {
        if (zone != _activeZone) return;

        Debug.Log($"[EnemySpawner] Zone exited: {zone.areaConfig?.areaName} — waiting for new zone");

        if (_zoneExitCoroutine != null) StopCoroutine(_zoneExitCoroutine);
        _zoneExitCoroutine = StartCoroutine(WaitForNewZone(zone));
    }

    // Waits until end of physics step (WaitForFixedUpdate) so any adjacent
    // OnTriggerEnter has had a chance to fire before we stop spawning.
    private IEnumerator WaitForNewZone(SpawnZone exitedZone)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate(); // two steps to be safe

        if (_activeZone != exitedZone)
        {
            Debug.Log($"[EnemySpawner] New zone already active — exit ignored");
            _zoneExitCoroutine = null;
            yield break;
        }

        Debug.Log($"[EnemySpawner] No new zone entered — stopping spawns");
        StopAllCoroutines();
        _activeZone = null;
        _activeConfig = null;
        _zoneExitCoroutine = null;
    }

    // ── Zone activation ────────────────────────────────────────
    private void ActivateZone(SpawnZone zone)
    {
        if (zone.areaConfig == null)
        {
            Debug.LogWarning($"[EnemySpawner] Zone {zone.name} has no AreaZoneConfig assigned.");
            return;
        }

        _activeZone = zone;
        _activeConfig = zone.areaConfig;
        _activeLeftPoints = zone.leftSpawnPoints;
        _activeRightPoints = zone.rightSpawnPoints;

        // Validate points exist
        if (_activeLeftPoints == null || _activeLeftPoints.Length == 0)
            Debug.LogWarning($"[EnemySpawner] Zone {zone.name} has no LEFT spawn points.");
        if (_activeRightPoints == null || _activeRightPoints.Length == 0)
            Debug.LogWarning($"[EnemySpawner] Zone {zone.name} has no RIGHT spawn points.");

        // Fisher-Yates shuffle for this zone
        _shuffledLeft = FisherYatesShuffle(_activeLeftPoints);
        _shuffledRight = FisherYatesShuffle(_activeRightPoints);
        _leftIdx = 0;
        _rightIdx = 0;

        // Reset per-type counts AND side totals for new zone config
        _activeCountsLeft.Clear();
        _activeCountsRight.Clear();
        _activeLeft = 0;
        _activeRight = 0;

        // Restart spawn loops
        StopAllCoroutines();
        StartCoroutine(SpawnLoop(SpawnSide.Left));
        StartCoroutine(SpawnLoop(SpawnSide.Right));

        Debug.Log($"[EnemySpawner] Activated: {_activeConfig.areaName} " +
                  $"| LeftPoints={_activeLeftPoints?.Length ?? 0} " +
                  $"| RightPoints={_activeRightPoints?.Length ?? 0}");
    }

    // ── Fisher-Yates ───────────────────────────────────────────
    private Transform[] FisherYatesShuffle(Transform[] source)
    {
        if (source == null || source.Length == 0) return new Transform[0];

        var arr = (Transform[])source.Clone();
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }

    private Vector3 GetNextSpawnPoint(SpawnSide side)
    {
        bool isLeft = side == SpawnSide.Left;
        Transform[] source = isLeft ? _activeLeftPoints : _activeRightPoints;
        Transform[] shuffled = isLeft ? _shuffledLeft : _shuffledRight;

        if (source == null || source.Length == 0)
        {
            Debug.LogWarning($"[EnemySpawner] No spawn points for {side}");
            return Vector3.zero;
        }

        int idx = isLeft ? _leftIdx : _rightIdx;

        // Re-shuffle index order only when full cycle complete
        if (idx >= source.Length)
        {
            shuffled = FisherYatesShuffle(source);
            if (isLeft) { _shuffledLeft = shuffled; _leftIdx = 0; idx = 0; }
            else { _shuffledRight = shuffled; _rightIdx = 0; idx = 0; }
        }

        // Pick the transform at shuffled index — transform itself is never moved
        Transform point = shuffled[idx];
        if (isLeft) _leftIdx++;
        else _rightIdx++;

        if (point == null) return Vector3.zero;

        // Snap directly to NavMesh at exact transform position — no jitter
        if (NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;

        Debug.LogWarning($"[EnemySpawner] No NavMesh near spawn point {point.name}");
        return Vector3.zero;
    }

    // ── Spawn loops ────────────────────────────────────────────
    private IEnumerator SpawnLoop(SpawnSide side)
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            TrySpawnOnSide(side);
        }
    }

    private void TrySpawnOnSide(SpawnSide side)
    {
        if (_activeConfig == null) return;

        SideSpawnConfig sideConfig = _activeConfig.GetSideConfig(side);
        if (sideConfig == null) return;

        int active = side == SpawnSide.Left ? _activeLeft : _activeRight;
        var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;

        if (active >= sideConfig.RespawnThreshold) return;
        if (active >= sideConfig.maxTotalActive) return;

        SideTypeEntry entry = sideConfig.GetRandomEntry(counts);
        if (entry == null)
        {
            Debug.LogWarning($"[EnemySpawner] {side}: GetRandomEntry null " +
                             $"— check entries/weights in {_activeConfig.areaName}");
            return;
        }

        Vector3 spawnPos = GetNextSpawnPoint(side);
        if (spawnPos == Vector3.zero) return;

        // Debug.Log($"[EnemySpawner] Spawning {entry.prefab?.name} on {side} at {spawnPos}");
        SpawnEnemy(entry, side, spawnPos, fromSummoner: false);
    }

    // ── Despawn all enemies from a specific zone ───────────────
    private void DespawnZoneEnemies(SpawnZone zone)
    {
        var toRemove = new System.Collections.Generic.List<GameObject>();
        foreach (var go in _allActive)
        {
            if (go == null) continue;
            var tracker = go.GetComponent<ZoneEnemyTracker>();
            if (tracker != null && tracker.HomeZone == zone)
                toRemove.Add(go);
        }

        Debug.Log($"[EnemySpawner] Despawning {toRemove.Count} enemies from old zone {zone.areaConfig?.areaName}");

        foreach (var go in toRemove)
        {
            var enemy = go.GetComponent<Enemy>();
            enemy?.Health?.TakeDamage(new DamageData(99999f, DamageType.Environmental));
        }
    }

    // ── Actual spawn ───────────────────────────────────────────
    public void SpawnEnemy(SideTypeEntry entry, SpawnSide side,
                       Vector3 position, bool fromSummoner = false)
    {
        if (_pool == null || entry?.prefab == null) return;

        GameObject instance = _pool.Get(entry.prefab);
        if (instance == null) return;

        // Set position then re-enable agent — order matters
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;

        var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(position);
            agent.enabled = true;
        }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null) { _pool.Return(entry.prefab, instance); return; }

        // Wire pool return — without this, death calls Destroy() and counts never decrement
        enemy.SetPoolProvider(_pool, entry.prefab);

        if (enemy is ITimeAffected ta)
            timeFactorBootstrapper?.RegisterEntity(ta);

        if (entry.data != null) enemy.ApplyData(entry.data);

        // Zone tracker for home-zone return on sight loss
        var tracker = instance.GetComponent<ZoneEnemyTracker>();
        if (tracker != null)
        {
            tracker.ResetForPool();
            tracker.HomeZone = _activeZone;
        }

        if (enemy is GroupGrabEnemy grabEnemy)
            _rescueRegistry?.RegisterTrap(grabEnemy);

        _allActive.Add(instance);

        // Register with EnemyDeathNotifier — no static event needed
        deathNotifier?.Register(enemy.Health);

        if (!fromSummoner)
        {
            var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
            if (!counts.ContainsKey(entry)) counts[entry] = 0;
            counts[entry]++;

            if (side == SpawnSide.Left) _activeLeft++;
            else _activeRight++;
        }

        enemy.Health.OnDeath += () => HandleEnemyDeath(entry, side, instance, fromSummoner);
    }

    private void HandleEnemyDeath(SideTypeEntry entry, SpawnSide side,
                                   GameObject instance, bool fromSummoner)
    {
        _allActive.Remove(instance);

        var enemy = instance.GetComponent<Enemy>();
        if (enemy != null)
        {
            if (enemy is ITimeAffected ta) timeFactorBootstrapper?.UnregisterEntity(ta);
            deathNotifier?.Unregister(enemy.Health);
        }

        if (!fromSummoner)
        {
            var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
            if (counts.ContainsKey(entry))
                counts[entry] = Mathf.Max(0, counts[entry] - 1);

            if (side == SpawnSide.Left) _activeLeft = Mathf.Max(0, _activeLeft - 1);
            else _activeRight = Mathf.Max(0, _activeRight - 1);
        }
    }

    // ── Summoner API ───────────────────────────────────────────
    public void SummonerSpawn(SideTypeEntry entry, Vector3 position)
    {
        if (_pool == null || entry?.prefab == null) return;

        GameObject instance = _pool.Get(entry.prefab);
        if (instance == null) return;

        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;

        // FIX: warp agent — without this summoned enemies can't move
        var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(position);
            agent.enabled = true;
        }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null) { _pool.Return(entry.prefab, instance); return; }

        enemy.SetPoolProvider(_pool, entry.prefab);

        if (enemy is ITimeAffected ta)
            timeFactorBootstrapper?.RegisterEntity(ta);

        if (entry.data != null) enemy.ApplyData(entry.data);

        var tracker = instance.GetComponent<ZoneEnemyTracker>();
        if (tracker != null)
        {
            tracker.ResetForPool();
            tracker.HomeZone = _activeZone;
        }

        if (enemy is GroupGrabEnemy grabEnemy)
            _rescueRegistry?.RegisterTrap(grabEnemy);

        _allActive.Add(instance);
        deathNotifier?.Register(enemy.Health);
        enemy.Health.OnDeath += () =>
        {
            _allActive.Remove(instance);
            deathNotifier?.Unregister(enemy.Health);
            var e = instance.GetComponent<Enemy>();
            if (e is ITimeAffected t) timeFactorBootstrapper?.UnregisterEntity(t);
        };
    }

    // ── NavMesh ────────────────────────────────────────────────
    // ── Pause/Resume ───────────────────────────────────────────
    public void PauseSpawning() => StopAllCoroutines();
    public void ResumeSpawning()
    {
        if (_activeConfig != null)
        {
            StartCoroutine(SpawnLoop(SpawnSide.Left));
            StartCoroutine(SpawnLoop(SpawnSide.Right));
        }
    }

    public SpawnSide GetSideForPosition(Vector3 worldPos)
    {
        if (barrierTransform == null) return SpawnSide.Left;
        return worldPos.x < barrierTransform.position.x ? SpawnSide.Left : SpawnSide.Right;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_activeLeftPoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (var p in _activeLeftPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.5f);
        }
        if (_activeRightPoints != null)
        {
            Gizmos.color = Color.red;
            foreach (var p in _activeRightPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.5f);
        }
    }
#endif
}