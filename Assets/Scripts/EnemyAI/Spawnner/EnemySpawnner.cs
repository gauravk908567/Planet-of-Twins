using System;
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
    [SerializeField] private Player _leftPlayer;
    [SerializeField] private Player _rightPlayer;
    [SerializeField] private SoulPlayer _soulPlayer;

    // ── Runtime ────────────────────────────────────────────────
    private IEnemyPoolProvider _pool;
    private IRescueTrapRegistry _rescueRegistry;

    private SpawnZone _activeZone;
    private AreaZoneConfig _activeConfig;
    private Transform[] _activeLeftPoints;
    private Transform[] _activeRightPoints;

    private Transform[] _shuffledLeft;
    private Transform[] _shuffledRight;
    private int _leftIdx;
    private int _rightIdx;

    private int _activeLeft;
    private int _activeRight;
    private readonly Dictionary<SideTypeEntry, int> _activeCountsLeft = new();
    private readonly Dictionary<SideTypeEntry, int> _activeCountsRight = new();
    private readonly HashSet<GameObject> _allActive = new();

    // Severed pair tracking — keyed by prefab, one pending per side
    private readonly Dictionary<GameObject, GameObject> _pendingSeveredLeft = new();
    private readonly Dictionary<GameObject, GameObject> _pendingSeveredRight = new();


    // FIX: store named delegates keyed by instance so we can -= before += on pool reuse.
    // The old approach used anonymous lambdas which can never be removed with -=,
    // causing OnDeath to accumulate one extra handler per reuse.
    // On reuse: old handler removed → new handler added → exactly 1 handler at all times.
    // IMPORTANT: we do NOT use ClearDeathSubscribers() because that fires mid-invocation
    // and would wipe the proxy's HandleKillerDied before it has a chance to run.
    private readonly Dictionary<GameObject, Action> _spawnDeathHandlers = new();

    // ── Unity lifecycle ────────────────────────────────────────
    private void Awake()
    {
        _pool = poolProviderObject as IEnemyPoolProvider;
        _rescueRegistry = rescueControllerObject as IRescueTrapRegistry;

        // Inject Siphon scene refs into pool so ghost spawning works
        if (_pool is EnemyPool enemyPool && _leftPlayer != null && _rightPlayer != null)
        {
            var rescue = rescueControllerObject as RescueEventController;
            enemyPool.SetSiphonReferences(_leftPlayer, _rightPlayer, _soulPlayer, rescue);
        }

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

        ActivateZone(zone);
    }

    private void HandleZoneExited(SpawnZone zone)
    {
        if (zone != _activeZone) return;

        Debug.Log($"[EnemySpawner] Zone exited: {zone.areaConfig?.areaName} — waiting for new zone");

        if (_zoneExitCoroutine != null) StopCoroutine(_zoneExitCoroutine);
        _zoneExitCoroutine = StartCoroutine(WaitForNewZone(zone));
    }

    private IEnumerator WaitForNewZone(SpawnZone exitedZone)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

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

        if (_activeLeftPoints == null || _activeLeftPoints.Length == 0)
            Debug.LogWarning($"[EnemySpawner] Zone {zone.name} has no LEFT spawn points.");
        if (_activeRightPoints == null || _activeRightPoints.Length == 0)
            Debug.LogWarning($"[EnemySpawner] Zone {zone.name} has no RIGHT spawn points.");

        _shuffledLeft = FisherYatesShuffle(_activeLeftPoints);
        _shuffledRight = FisherYatesShuffle(_activeRightPoints);
        _leftIdx = 0;
        _rightIdx = 0;

        _activeCountsLeft.Clear();
        _activeCountsRight.Clear();
        _activeLeft = 0;
        _activeRight = 0;
        _pendingSeveredLeft.Clear();
        _pendingSeveredRight.Clear();

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
            int j = UnityEngine.Random.Range(0, i + 1);
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

        if (idx >= source.Length)
        {
            shuffled = FisherYatesShuffle(source);
            if (isLeft) { _shuffledLeft = shuffled; _leftIdx = 0; idx = 0; }
            else { _shuffledRight = shuffled; _rightIdx = 0; idx = 0; }
        }

        Transform point = shuffled[idx];
        if (isLeft) _leftIdx++;
        else _rightIdx++;

        if (point == null) return Vector3.zero;

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

        // FIX: check hard cap first, then threshold.
        // RespawnThreshold is the low-water mark that triggers new spawns.
        // Old order blocked spawning whenever active >= threshold even
        // when enemies had died and count was still below the hard cap.
        if (active >= sideConfig.maxTotalActive) return;
        if (active > sideConfig.RespawnThreshold) return;

        SideTypeEntry entry = sideConfig.GetRandomEntry(counts);
        if (entry == null)
        {
            Debug.LogWarning($"[EnemySpawner] {side}: GetRandomEntry null " +
                             $"— check entries/weights in {_activeConfig.areaName}");
            return;
        }

        Vector3 spawnPos = GetNextSpawnPoint(side);
        if (spawnPos == Vector3.zero) return;

        SpawnEnemy(entry, side, spawnPos, fromSummoner: false);
    }

    // ── Despawn zone enemies ───────────────────────────────────
    private void DespawnZoneEnemies(SpawnZone zone)
    {
        var toRemove = new List<GameObject>();
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

        if (enemy is IRescueTarget rescueTarget)
            _rescueRegistry?.RegisterTrap(rescueTarget);

        // Severed pair wiring
        if (enemy is SeveredEnemy severed)
        {
            Debug.Log($"[Spawner] Severed detected side={side}");
            if (side == SpawnSide.Left)
            {
                _pendingSeveredLeft[entry.prefab] = instance;
                if (_pendingSeveredRight.TryGetValue(entry.prefab, out var rightGO))
                {
                    var rightSevered = rightGO?.GetComponent<SeveredEnemy>();
                    if (rightSevered != null)
                    {
                        severed.InitialisePair(rightSevered);
                        rightSevered.InitialisePair(severed);
                        _pendingSeveredLeft.Remove(entry.prefab);
                        _pendingSeveredRight.Remove(entry.prefab);
                        Debug.Log("[Spawner] Severed pair wired.");
                    }
                }
            }
            else
            {
                _pendingSeveredRight[entry.prefab] = instance;
                if (_pendingSeveredLeft.TryGetValue(entry.prefab, out var leftGO))
                {
                    var leftSevered = leftGO?.GetComponent<SeveredEnemy>();
                    if (leftSevered != null)
                    {
                        severed.InitialisePair(leftSevered);
                        leftSevered.InitialisePair(severed);
                        _pendingSeveredLeft.Remove(entry.prefab);
                        _pendingSeveredRight.Remove(entry.prefab);
                        Debug.Log("[Spawner] Severed pair wired.");
                    }
                }
            }
        }

        _allActive.Add(instance);
        deathNotifier?.Register(enemy.Health);

        if (!fromSummoner)
        {
            var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
            if (!counts.ContainsKey(entry)) counts[entry] = 0;
            counts[entry]++;

            if (side == SpawnSide.Left) _activeLeft++;
            else _activeRight++;
        }

        // FIX: Remove old handler if this instance was previously pooled.
        // The old lambda can never be removed with -= so we store it by instance key.
        // This prevents the accumulation bug (N reuses → N HandleEnemyDeath calls on death)
        // WITHOUT using ClearDeathSubscribers, which would fire mid-invocation and wipe
        // the proxy's HandleKillerDied before it resolves the rescue.
        if (_spawnDeathHandlers.TryGetValue(instance, out var oldHandler))
        {
            enemy.Health.OnDeath -= oldHandler;
            _spawnDeathHandlers.Remove(instance);
        }

        Action deathHandler = () => HandleEnemyDeath(entry, side, instance, fromSummoner);
        _spawnDeathHandlers[instance] = deathHandler;
        enemy.Health.OnDeath += deathHandler;
    }

    private void HandleEnemyDeath(SideTypeEntry entry, SpawnSide side,
                                   GameObject instance, bool fromSummoner)
    {
        _allActive.Remove(instance);
        _spawnDeathHandlers.Remove(instance);

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

        if (enemy is IRescueTarget rescueTarget)
            _rescueRegistry?.RegisterTrap(rescueTarget);

        _allActive.Add(instance);
        deathNotifier?.Register(enemy.Health);

        // Same named-delegate fix as SpawnEnemy
        if (_spawnDeathHandlers.TryGetValue(instance, out var oldHandler))
        {
            enemy.Health.OnDeath -= oldHandler;
            _spawnDeathHandlers.Remove(instance);
        }

        Action summonerDeathHandler = () =>
        {
            _allActive.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
            deathNotifier?.Unregister(enemy.Health);
            var e = instance.GetComponent<Enemy>();
            if (e is ITimeAffected t) timeFactorBootstrapper?.UnregisterEntity(t);
        };

        _spawnDeathHandlers[instance] = summonerDeathHandler;
        enemy.Health.OnDeath += summonerDeathHandler;
    }

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