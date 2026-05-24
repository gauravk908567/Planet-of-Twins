using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Zones")]
    [SerializeField] private SpawnZone[] allZones;

    [Header("References")]
    [SerializeField] private MonoBehaviour poolProviderObject;
    [SerializeField] private TimeFactorBootstrapper timeFactorBootstrapper;
    [SerializeField] private Transform barrierTransform;
    [SerializeField] private EnemyDeathNotifier deathNotifier;
    [SerializeField] private MonoBehaviour rescueControllerObject;
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

    // Pair tracking — pending partner keyed by prefab per side
    private readonly Dictionary<GameObject, GameObject> _pendingBondLeft = new();
    private readonly Dictionary<GameObject, GameObject> _pendingBondRight = new();

    // Severed still needs left+right for shared health mechanic
    private readonly Dictionary<GameObject, GameObject> _pendingSeveredLeft = new();
    private readonly Dictionary<GameObject, GameObject> _pendingSeveredRight = new();

    private readonly Dictionary<GameObject, Action> _spawnDeathHandlers = new();

    private int _activeGroupCount = 0;
    private Coroutine _zoneExitCoroutine;

    // ── Lifecycle ──────────────────────────────────────────────
    private void Awake()
    {
        _pool = poolProviderObject as IEnemyPoolProvider;
        _rescueRegistry = rescueControllerObject as IRescueTrapRegistry;

        if (_pool is EnemyPool ep && _leftPlayer != null)
        {
            var rescue = rescueControllerObject as RescueEventController;
            ep.SetSiphonReferences(_leftPlayer, _rightPlayer, _soulPlayer, rescue);
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
    private void HandleZoneEntered(SpawnZone zone)
    {
        if (zone == _activeZone) return;
        if (_zoneExitCoroutine != null) { StopCoroutine(_zoneExitCoroutine); _zoneExitCoroutine = null; }
        ActivateZone(zone);
    }

    private void HandleZoneExited(SpawnZone zone)
    {
        if (zone != _activeZone) return;
        if (_zoneExitCoroutine != null) StopCoroutine(_zoneExitCoroutine);
        _zoneExitCoroutine = StartCoroutine(WaitForNewZone(zone));
    }

    private IEnumerator WaitForNewZone(SpawnZone exitedZone)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_activeZone != exitedZone) { _zoneExitCoroutine = null; yield break; }
        StopAllCoroutines();
        _activeZone = null; _activeConfig = null; _zoneExitCoroutine = null;
    }

    // ── Zone activation ────────────────────────────────────────
    private void ActivateZone(SpawnZone zone)
    {
        if (zone.areaConfig == null) return;

        _activeZone = zone;
        _activeConfig = zone.areaConfig;
        _activeLeftPoints = zone.leftSpawnPoints;
        _activeRightPoints = zone.rightSpawnPoints;

        _shuffledLeft = Shuffle(_activeLeftPoints);
        _shuffledRight = Shuffle(_activeRightPoints);
        _leftIdx = _rightIdx = 0;

        _activeCountsLeft.Clear(); _activeCountsRight.Clear();
        _activeLeft = _activeRight = 0;
        _pendingBondLeft.Clear(); _pendingBondRight.Clear();
        _pendingSeveredLeft.Clear(); _pendingSeveredRight.Clear();
        _activeGroupCount = 0;

        StopAllCoroutines();
        StartCoroutine(SpawnLoop(SpawnSide.Left));
        StartCoroutine(SpawnLoop(SpawnSide.Right));

        TrySpawnCommanderGroup();
        SyncClanWarToBlackboard();
    }

    private void SyncClanWarToBlackboard()
    {
        if (_activeConfig == null) return;
        var shared = CommonCore.BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        if (shared == null) return;
        shared.Set(PoTNames.ClanWarActive, _activeConfig.clanWarActive);
        shared.Set(PoTNames.ClanWarIntensity, _activeConfig.clanWarIntensity);
    }

    // ── Spawn loops ────────────────────────────────────────────
    private IEnumerator SpawnLoop(SpawnSide side)
    {
        while (true)
        {
            yield return new WaitForSeconds(_activeConfig?.spawnInterval ?? 3f);
            TrySpawnOnSide(side);
        }
    }

    private void TrySpawnOnSide(SpawnSide side)
    {
        if (_activeConfig == null) return;
        var cfg = _activeConfig.GetSideConfig(side);
        if (cfg == null) return;

        int active = side == SpawnSide.Left ? _activeLeft : _activeRight;
        if (active >= cfg.maxTotalActive) return;
        if (active >= cfg.RespawnThreshold) return;

        var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
        var entry = cfg.GetRandomEntry(counts);
        if (entry == null) return;

        SpawnEnemy(entry, side, fromSummoner: false);
    }

    // ── Core spawn ─────────────────────────────────────────────
    public void SpawnEnemy(SideTypeEntry entry, SpawnSide side, bool fromSummoner)
    {
        if (_pool == null || entry?.prefab == null) return;

        Vector3 pos = GetNextSpawnPoint(side);
        if (pos == Vector3.zero) return;

        var instance = _pool.Get(entry.prefab);
        if (instance == null) return;

        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.identity;

        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null) { agent.Warp(pos); agent.enabled = true; }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null) { _pool.Return(entry.prefab, instance); return; }

        enemy.SetPoolProvider(_pool, entry.prefab);
        if (enemy is ITimeAffected ta) timeFactorBootstrapper?.RegisterEntity(ta);
        if (entry.data != null) enemy.ApplyData(entry.data);

        if (entry.HasDarkEnergyOverride)
            instance.GetComponent<EnemyDarkEnergy>()
                ?.ApplyLevelScaling(entry.darkEnergyBase, entry.bondBreakThreshold);

        var tracker = instance.GetComponent<ZoneEnemyTracker>();
        if (tracker != null) { tracker.ResetForPool(); tracker.HomeZone = _activeZone; }

        if (enemy is IRescueTarget rt) _rescueRegistry?.RegisterTrap(rt);

        // ── Pair wiring ────────────────────────────────────────
        if (enemy is SeveredEnemy severed)
            WireSeveredPair(severed, entry.prefab, side);
        else if (entry.pairConfig != null)
            TrySpawnPartner(entry, enemy, side);

        _allActive.Add(instance);
        deathNotifier?.Register(enemy.Health);
        RegisterDeathHandler(instance, enemy, entry, side, fromSummoner);

        if (!fromSummoner)
        {
            var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
            if (!counts.ContainsKey(entry)) counts[entry] = 0;
            counts[entry]++;
            if (side == SpawnSide.Left) _activeLeft++; else _activeRight++;
        }
    }

    // ── Partner spawning ───────────────────────────────────────
    private void TrySpawnPartner(SideTypeEntry primaryEntry, Enemy primaryEnemy, SpawnSide primarySide)
    {
        var partnerEntry = primaryEntry.pairConfig.PickPartner();
        if (partnerEntry == null) return;

        SpawnSide partnerSide = partnerEntry.sameSide
            ? primarySide
            : (primarySide == SpawnSide.Left ? SpawnSide.Right : SpawnSide.Left);

        Vector3 partnerPos = GetNextSpawnPoint(partnerSide);
        if (partnerPos == Vector3.zero) return;

        var instance = _pool.Get(partnerEntry.partnerPrefab);
        if (instance == null) return;

        instance.transform.position = partnerPos;
        instance.transform.rotation = Quaternion.identity;

        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null) { agent.Warp(partnerPos); agent.enabled = true; }

        var partnerEnemy = instance.GetComponent<Enemy>();
        if (partnerEnemy == null) { _pool.Return(partnerEntry.partnerPrefab, instance); return; }

        partnerEnemy.SetPoolProvider(_pool, partnerEntry.partnerPrefab);
        if (partnerEnemy is ITimeAffected pta) timeFactorBootstrapper?.RegisterEntity(pta);
        if (partnerEntry.partnerData != null) partnerEnemy.ApplyData(partnerEntry.partnerData);

        var tracker = instance.GetComponent<ZoneEnemyTracker>();
        if (tracker != null) { tracker.ResetForPool(); tracker.HomeZone = _activeZone; }

        if (partnerEnemy is IRescueTarget prt) _rescueRegistry?.RegisterTrap(prt);

        // Wire social bonds
        WireBond(primaryEnemy, partnerEnemy, partnerEntry.bondType);

        _allActive.Add(instance);
        deathNotifier?.Register(partnerEnemy.Health);

        Action death = () =>
        {
            _allActive.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
            deathNotifier?.Unregister(partnerEnemy.Health);
            if (partnerEnemy is ITimeAffected t) timeFactorBootstrapper?.UnregisterEntity(t);
            instance.GetComponent<EnemySocialBond>()?.ClearBond();
        };
        _spawnDeathHandlers[instance] = death;
        partnerEnemy.Health.OnDeath += death;

        Debug.Log($"[Spawner] Pair: {primaryEnemy.name} ↔ {partnerEnemy.name} ({partnerEntry.bondType})");
    }

    private void WireBond(Enemy a, Enemy b, EnemySocialBond.BondType bondType)
    {
        a.GetComponent<EnemySocialBond>()?.SetBondPartner(b, bondType);
        b.GetComponent<EnemySocialBond>()?.SetBondPartner(a, bondType);
    }

    // ── Severed pair ───────────────────────────────────────────
    private void WireSeveredPair(SeveredEnemy severed, GameObject prefab, SpawnSide side)
    {
        var pendingThis = side == SpawnSide.Left ? _pendingSeveredLeft : _pendingSeveredRight;
        var pendingOther = side == SpawnSide.Left ? _pendingSeveredRight : _pendingSeveredLeft;

        pendingThis[prefab] = severed.gameObject;

        if (pendingOther.TryGetValue(prefab, out var otherGO))
        {
            var other = otherGO?.GetComponent<SeveredEnemy>();
            if (other != null)
            {
                severed.InitialisePair(other);
                other.InitialisePair(severed);
                pendingThis.Remove(prefab);
                pendingOther.Remove(prefab);
                Debug.Log("[Spawner] Severed pair wired.");
            }
        }
    }

    // ── Commander group spawning ───────────────────────────────
    private void TrySpawnCommanderGroup()
    {
        if (_activeConfig?.groupSpawn == null) return;
        if (_activeGroupCount >= _activeConfig.groupSpawn.maxActiveGroups) return;

        var groupDef = _activeConfig.groupSpawn.PickGroup();
        if (groupDef == null) return;

        StartCoroutine(SpawnCommanderGroup(groupDef));
    }

    private IEnumerator SpawnCommanderGroup(CommanderGroupDefinition groupDef)
    {
        _activeGroupCount++;

        // Spawn commander
        Vector3 commanderPos = GetNextSpawnPoint(groupDef.spawnSide);
        if (commanderPos == Vector3.zero) { _activeGroupCount--; yield break; }

        var commanderInstance = _pool.Get(groupDef.commanderPrefab);
        if (commanderInstance == null) { _activeGroupCount--; yield break; }

        commanderInstance.transform.position = commanderPos;
        var cAgent = commanderInstance.GetComponent<NavMeshAgent>();
        if (cAgent != null) { cAgent.Warp(commanderPos); cAgent.enabled = true; }

        // Need both Enemy (for pool/health/data) and ICommander (for group ops)
        var commanderEnemy = commanderInstance.GetComponent<Enemy>();
        var commander = commanderInstance.GetComponent<ICommander>();

        if (commanderEnemy == null || commander == null)
        {
            _pool.Return(groupDef.commanderPrefab, commanderInstance);
            _activeGroupCount--;
            yield break;
        }

        commanderEnemy.SetPoolProvider(_pool, groupDef.commanderPrefab);
        if (groupDef.commanderData != null) commanderEnemy.ApplyData(groupDef.commanderData);

        // Initialise commander values — each type has InitialiseCommander
        if (commanderInstance.GetComponent<ChainCommander>() is ChainCommander cc)
            cc.InitialiseCommander(groupDef.commandRadius, groupDef.commanderDeathRageDuration);
        else if (commanderInstance.GetComponent<GrandSummoner>() is GrandSummoner gs)
            gs.InitialiseCommander(groupDef.commandRadius, groupDef.commanderDeathRageDuration);
        else if (commanderInstance.GetComponent<PenitentCommander>() is PenitentCommander pc)
            pc.InitialiseCommander(groupDef.commandRadius, groupDef.commanderDeathRageDuration);

        var cTracker = commanderInstance.GetComponent<ZoneEnemyTracker>();
        if (cTracker != null) { cTracker.ResetForPool(); cTracker.HomeZone = _activeZone; }

        _allActive.Add(commanderInstance);
        deathNotifier?.Register(commanderEnemy.Health);

        commanderEnemy.Health.OnDeath += () =>
        {
            _allActive.Remove(commanderInstance);
            deathNotifier?.Unregister(commanderEnemy.Health);
            StartCoroutine(GroupRespawnDelay(groupDef));
        };

        yield return new WaitForSeconds(0.5f); // brief delay before soldiers

        // Spawn soldiers
        foreach (var slot in groupDef.slots)
        {
            if (slot?.prefab == null) continue;

            Vector3 slotWorld = commander.GetSlotWorldPosition(slot.offset);
            if (NavMesh.SamplePosition(slotWorld, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                slotWorld = hit.position;

            var soldierInstance = _pool.Get(slot.prefab);
            if (soldierInstance == null) continue;

            soldierInstance.transform.position = slotWorld;
            var sAgent = soldierInstance.GetComponent<NavMeshAgent>();
            if (sAgent != null) { sAgent.Warp(slotWorld); sAgent.enabled = true; }

            var soldierEnemy = soldierInstance.GetComponent<Enemy>();
            if (soldierEnemy == null) { _pool.Return(slot.prefab, soldierInstance); continue; }

            soldierEnemy.SetPoolProvider(_pool, slot.prefab);
            if (slot.data != null) soldierEnemy.ApplyData(slot.data);

            var sTracker = soldierInstance.GetComponent<ZoneEnemyTracker>();
            if (sTracker != null) { sTracker.ResetForPool(); sTracker.HomeZone = _activeZone; }

            // Register with commander — wires GOAPGoalHoldFormation
            commander.RegisterSoldier(soldierEnemy);

            // Set slot offset on formation goal
            soldierInstance.GetComponent<GOAPGoalHoldFormation>()
                ?.SetCommander(commander, slot.offset);

            // Wire PenitentCommander damage notification
            commanderInstance.GetComponent<GOAPBrainPenitentCommander>()
                ?.WireSoldierDamage(soldierEnemy);

            _allActive.Add(soldierInstance);
            deathNotifier?.Register(soldierEnemy.Health);

            soldierEnemy.Health.OnDeath += () =>
            {
                _allActive.Remove(soldierInstance);
                deathNotifier?.Unregister(soldierEnemy.Health);
            };

            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log($"[Spawner] Commander group spawned: {groupDef.name}");
    }

    private IEnumerator GroupRespawnDelay(CommanderGroupDefinition groupDef)
    {
        yield return new WaitForSeconds(_activeConfig?.groupSpawn?.groupRespawnDelay ?? 30f);
        _activeGroupCount--;
        TrySpawnCommanderGroup();
    }

    // ── Death ──────────────────────────────────────────────────
    private void RegisterDeathHandler(GameObject instance, Enemy enemy,
                                       SideTypeEntry entry, SpawnSide side, bool fromSummoner)
    {
        if (_spawnDeathHandlers.TryGetValue(instance, out var old))
        {
            enemy.Health.OnDeath -= old;
            _spawnDeathHandlers.Remove(instance);
        }

        Action handler = () =>
        {
            _allActive.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
            if (enemy is ITimeAffected ta) timeFactorBootstrapper?.UnregisterEntity(ta);
            deathNotifier?.Unregister(enemy.Health);
            instance.GetComponent<EnemySocialBond>()?.ClearBond();

            if (!fromSummoner)
            {
                var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
                if (counts.ContainsKey(entry)) counts[entry] = Mathf.Max(0, counts[entry] - 1);
                if (side == SpawnSide.Left) _activeLeft = Mathf.Max(0, _activeLeft - 1);
                else _activeRight = Mathf.Max(0, _activeRight - 1);
            }
        };

        _spawnDeathHandlers[instance] = handler;
        enemy.Health.OnDeath += handler;
    }

    // ── Summoner API ───────────────────────────────────────────
    public void SummonerSpawn(SideTypeEntry entry, Vector3 position)
    {
        if (_pool == null || entry?.prefab == null) return;

        var instance = _pool.Get(entry.prefab);
        if (instance == null) return;

        instance.transform.position = position;
        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null) { agent.Warp(position); agent.enabled = true; }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null) { _pool.Return(entry.prefab, instance); return; }

        enemy.SetPoolProvider(_pool, entry.prefab);
        if (enemy is ITimeAffected ta) timeFactorBootstrapper?.RegisterEntity(ta);
        if (entry.data != null) enemy.ApplyData(entry.data);

        if (entry.HasDarkEnergyOverride)
            instance.GetComponent<EnemyDarkEnergy>()
                ?.ApplyLevelScaling(entry.darkEnergyBase, entry.bondBreakThreshold);

        var tracker = instance.GetComponent<ZoneEnemyTracker>();
        if (tracker != null) { tracker.ResetForPool(); tracker.HomeZone = _activeZone; }

        if (enemy is IRescueTarget rt) _rescueRegistry?.RegisterTrap(rt);

        _allActive.Add(instance);
        deathNotifier?.Register(enemy.Health);

        Action death = () =>
        {
            _allActive.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
            deathNotifier?.Unregister(enemy.Health);
            if (enemy is ITimeAffected t) timeFactorBootstrapper?.UnregisterEntity(t);
        };
        _spawnDeathHandlers[instance] = death;
        enemy.Health.OnDeath += death;
    }

    // ── Helpers ────────────────────────────────────────────────
    public void PauseSpawning() => StopAllCoroutines();

    public void ResumeSpawning()
    {
        if (_activeConfig == null) return;
        StartCoroutine(SpawnLoop(SpawnSide.Left));
        StartCoroutine(SpawnLoop(SpawnSide.Right));
    }

    public SpawnSide GetSideForPosition(Vector3 pos)
    {
        if (barrierTransform == null) return SpawnSide.Left;
        return pos.x < barrierTransform.position.x ? SpawnSide.Left : SpawnSide.Right;
    }

    private Transform[] Shuffle(Transform[] source)
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

        if (source == null || source.Length == 0) return Vector3.zero;

        int idx = isLeft ? _leftIdx : _rightIdx;
        if (idx >= source.Length)
        {
            shuffled = Shuffle(source);
            if (isLeft) { _shuffledLeft = shuffled; _leftIdx = 0; idx = 0; }
            else { _shuffledRight = shuffled; _rightIdx = 0; idx = 0; }
        }

        Transform point = shuffled[idx];
        if (isLeft) _leftIdx++; else _rightIdx++;
        if (point == null) return Vector3.zero;

        return NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position : Vector3.zero;
    }
}