using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    // allZones removed — SpawnZones now self-register via SpawnZoneRegistry (multi-scene safe).

    [Header("References")]
    [SerializeField] private MonoBehaviour poolProviderObject;
    [SerializeField] private TimeFactorBootstrapper timeFactorBootstrapper;
    [SerializeField] private EnemyDeathNotifier deathNotifier;
    [SerializeField] private MonoBehaviour rescueControllerObject;
    [SerializeField] private Player _leftPlayer;
    [SerializeField] private Player _rightPlayer;
    [SerializeField] private SoulPlayer _soulPlayer;

    [Header("Spawn placement")]
    [Tooltip("Enemies materialise in a short NavMesh-sampled ring around the spawn point instead of " +
             "exactly ON it — the spawn VFX plays at the point, the body appears beside it (BUG-070).")]
    [SerializeField] private float _spawnScatterRadius = 2f;

    [Header("Debug")]
    [Tooltip("Per-interval spawn skip-reason + spawn-success logs (can be spammy). Zone-activation and " +
             "failure warnings always log regardless of this toggle.")]
    [SerializeField] private bool _debugSpawns = true;

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
    // Tracks which prefab each active enemy instance came from (pool return in DespawnAll/DespawnZone)
    private readonly Dictionary<GameObject, GameObject> _activePrefabMap = new();
    // Tracks which zone spawned each instance (needed for zone-scoped despawn on area unload)
    private readonly Dictionary<GameObject, SpawnZone> _instanceZoneMap = new();

    private int _activeGroupCount = 0;
    private Coroutine _zoneExitCoroutine;
    // True after Start() runs — guards OnEnable from subscribing before SpawnZoneRegistry is initialized
    private bool _started;

    // ── Singleton ──────────────────────────────────────────────
    public static EnemySpawner Instance { get; private set; }

    // ── Lifecycle ──────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _pool = poolProviderObject as IEnemyPoolProvider;
        _rescueRegistry = rescueControllerObject as IRescueTrapRegistry;

        if (_pool is EnemyPool ep && _leftPlayer != null)
        {
            var rescue = rescueControllerObject as RescueEventController;
            ep.SetSiphonReferences(_leftPlayer, _rightPlayer, _soulPlayer, rescue);
        }
    }

    private void Start()
    {
        if (SpawnZoneRegistry.Instance == null)
        {
            Debug.LogError("[EnemySpawner] SpawnZoneRegistry.Instance is null — is Persistent loaded?", this);
            enabled = false;
            return;
        }
        _started = true;
        SubscribeRegistry();

        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.OnLocationWillUnload += HandleLocationWillUnload;
        else
            Debug.LogError("[EnemySpawner] SceneFlowManager.Instance is null — area unload despawn disabled.", this);
    }

    private void OnEnable()
    {
        if (!_started) return;
        SubscribeRegistry();
    }

    private void OnDisable()
    {
        UnsubscribeRegistry();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeRegistry();
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.OnLocationWillUnload -= HandleLocationWillUnload;
    }

    private void SubscribeRegistry()
    {
        if (SpawnZoneRegistry.Instance == null) return;
        SpawnZoneRegistry.Instance.OnZoneRegistered   += HandleZoneRegistered;
        SpawnZoneRegistry.Instance.OnZoneUnregistered += HandleZoneUnregistered;
        foreach (var zone in SpawnZoneRegistry.Instance.RegisteredZones)
            HandleZoneRegistered(zone);
    }

    private void UnsubscribeRegistry()
    {
        if (SpawnZoneRegistry.Instance == null) return;
        SpawnZoneRegistry.Instance.OnZoneRegistered   -= HandleZoneRegistered;
        SpawnZoneRegistry.Instance.OnZoneUnregistered -= HandleZoneUnregistered;
    }

    private void HandleZoneRegistered(SpawnZone zone)
    {
        if (zone == null) return;
        zone.OnZoneEntered += HandleZoneEntered;
        zone.OnZoneExited  += HandleZoneExited;
    }

    private void HandleZoneUnregistered(SpawnZone zone)
    {
        if (zone == null) return;
        zone.OnZoneEntered -= HandleZoneEntered;
        zone.OnZoneExited  -= HandleZoneExited;
        if (_activeZone == zone) { StopAllCoroutines(); _activeZone = null; _activeConfig = null; }
        DespawnZone(zone);
    }

    private void HandleLocationWillUnload(WorldLocationSO location)
    {
        var zones = SpawnZoneRegistry.Instance?.RegisteredZones;
        if (zones == null) return;
        foreach (var zone in zones.ToList())
        {
            if (zone == null) continue;
            if (zone.gameObject.scene.name == location.scene.Name)
                DespawnZone(zone);
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
        if (zone.areaConfig == null)
        {
            Debug.LogWarning($"[EnemySpawner] Zone '{zone.name}' entered but has NO AreaZoneConfig — " +
                             "cannot spawn anything here. Assign areaConfig on the SpawnZone.", zone);
            return;
        }

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

        Debug.Log($"[EnemySpawner] Zone '{zone.name}' ACTIVATED — spawn loops started. " +
                  $"interval={_activeConfig.spawnInterval}s, leftPoints={_activeLeftPoints?.Length ?? 0}, " +
                  $"rightPoints={_activeRightPoints?.Length ?? 0}.", zone);

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
        if (_activeConfig == null)
        { if (_debugSpawns) Debug.Log($"[SpawnDebug] {side}: no active config — skip."); return; }

        var cfg = _activeConfig.GetSideConfig(side);
        if (cfg == null)
        { if (_debugSpawns) Debug.Log($"[SpawnDebug] {side}: no side config on '{_activeConfig.name}' — skip."); return; }

        int active = side == SpawnSide.Left ? _activeLeft : _activeRight;
        if (active >= cfg.maxTotalActive)
        { if (_debugSpawns) Debug.Log($"[SpawnDebug] {side}: at maxTotalActive ({active}/{cfg.maxTotalActive}) — waiting for a kill."); return; }
        if (active >= cfg.RespawnThreshold)
        { if (_debugSpawns) Debug.Log($"[SpawnDebug] {side}: at RespawnThreshold ({active}/{cfg.RespawnThreshold}) — skip."); return; }

        var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
        var entry = cfg.GetRandomEntry(counts);
        if (entry == null)
        { if (_debugSpawns) Debug.Log($"[SpawnDebug] {side}: GetRandomEntry returned null (empty or exhausted type table) — skip."); return; }

        SpawnEnemy(entry, side, fromSummoner: false);
    }

    // ── Core spawn ─────────────────────────────────────────────
    public void SpawnEnemy(SideTypeEntry entry, SpawnSide side, bool fromSummoner)
    {
        if (_pool == null || entry?.prefab == null)
        {
            Debug.LogWarning($"[EnemySpawner] SpawnEnemy aborted on {side}: pool or entry.prefab is null.", this);
            return;
        }

        Vector3 pos = GetNextSpawnPoint(side);
        if (pos == Vector3.zero)
        {
            Debug.LogWarning($"[EnemySpawner] SpawnEnemy '{entry.prefab.name}' on {side}: no valid spawn " +
                             "position — no spawn points assigned, or NavMesh sampling missed. No enemy spawned.", this);
            return;
        }

        var instance = _pool.Get(entry.prefab);
        if (instance == null)
        {
            Debug.LogWarning($"[EnemySpawner] SpawnEnemy '{entry.prefab.name}' on {side}: pool returned null.", this);
            return;
        }

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
        _activePrefabMap[instance] = entry.prefab;
        _instanceZoneMap[instance] = _activeZone;
        deathNotifier?.Register(enemy.Health);
        RegisterDeathHandler(instance, enemy, entry, side, fromSummoner);

        if (!fromSummoner)
        {
            var counts = side == SpawnSide.Left ? _activeCountsLeft : _activeCountsRight;
            if (!counts.ContainsKey(entry)) counts[entry] = 0;
            counts[entry]++;
            if (side == SpawnSide.Left) _activeLeft++; else _activeRight++;
        }

        if (_debugSpawns)
            Debug.Log($"[EnemySpawner] SPAWNED '{entry.prefab.name}' on {side} at {pos} in zone " +
                      $"'{(_activeZone != null ? _activeZone.name : "?")}' " +
                      $"(activeLeft={_activeLeft}, activeRight={_activeRight}).", instance);
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
        _activePrefabMap[instance] = partnerEntry.partnerPrefab;
        _instanceZoneMap[instance] = _activeZone;
        deathNotifier?.Register(partnerEnemy.Health);

        Action death = () =>
        {
            _allActive.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
            _activePrefabMap.Remove(instance);
            _instanceZoneMap.Remove(instance);
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
        _instanceZoneMap[commanderInstance] = _activeZone;
        deathNotifier?.Register(commanderEnemy.Health);

        void CommanderDeathHandler()
        {
            if (_spawnDeathHandlers.TryGetValue(commanderInstance, out var h))
            {
                commanderEnemy.Health.OnDeath -= h;
                _spawnDeathHandlers.Remove(commanderInstance);
            }
            _allActive.Remove(commanderInstance);
            _instanceZoneMap.Remove(commanderInstance);
            _activePrefabMap.Remove(commanderInstance);
            deathNotifier?.Unregister(commanderEnemy.Health);
            StartCoroutine(GroupRespawnDelay(groupDef));
        }
        _spawnDeathHandlers[commanderInstance] = CommanderDeathHandler;
        commanderEnemy.Health.OnDeath += CommanderDeathHandler;

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
            _instanceZoneMap[soldierInstance] = _activeZone;
            deathNotifier?.Register(soldierEnemy.Health);

            void SoldierDeathHandler()
            {
                if (_spawnDeathHandlers.TryGetValue(soldierInstance, out var h))
                {
                    soldierEnemy.Health.OnDeath -= h;
                    _spawnDeathHandlers.Remove(soldierInstance);
                }
                _allActive.Remove(soldierInstance);
                _instanceZoneMap.Remove(soldierInstance);
                _activePrefabMap.Remove(soldierInstance);
                deathNotifier?.Unregister(soldierEnemy.Health);
            }
            _spawnDeathHandlers[soldierInstance] = SoldierDeathHandler;
            soldierEnemy.Health.OnDeath += SoldierDeathHandler;

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
            _activePrefabMap.Remove(instance);
            _instanceZoneMap.Remove(instance);
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
    public GameObject SummonerSpawn(SideTypeEntry entry, Vector3 position)
    {
        if (_pool == null || entry?.prefab == null) return null;

        var instance = _pool.Get(entry.prefab);
        if (instance == null) return null;

        instance.transform.position = position;
        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null) { agent.Warp(position); agent.enabled = true; }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null) { _pool.Return(entry.prefab, instance); return null; }

        // Summoned minion: the summoner's channel cue IS the spawn tell — skip the generic on_enemyspawn.
        enemy.SetPoolProvider(_pool, entry.prefab, playSpawnCue: false);
        if (enemy is ITimeAffected ta) timeFactorBootstrapper?.RegisterEntity(ta);
        if (entry.data != null) enemy.ApplyData(entry.data);

        if (entry.HasDarkEnergyOverride)
            instance.GetComponent<EnemyDarkEnergy>()
                ?.ApplyLevelScaling(entry.darkEnergyBase, entry.bondBreakThreshold);

        var tracker = instance.GetComponent<ZoneEnemyTracker>();
        if (tracker != null) { tracker.ResetForPool(); tracker.HomeZone = _activeZone; }

        if (enemy is IRescueTarget rt) _rescueRegistry?.RegisterTrap(rt);

        _allActive.Add(instance);
        _activePrefabMap[instance] = entry.prefab;
        _instanceZoneMap[instance] = _activeZone;
        deathNotifier?.Register(enemy.Health);

        Action death = () =>
        {
            _allActive.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
            _activePrefabMap.Remove(instance);
            _instanceZoneMap.Remove(instance);
            deathNotifier?.Unregister(enemy.Health);
            if (enemy is ITimeAffected t) timeFactorBootstrapper?.UnregisterEntity(t);
        };
        _spawnDeathHandlers[instance] = death;
        enemy.Health.OnDeath += death;
        return instance;
    }

    // ── Soft-reset API ─────────────────────────────────────────
    public void DespawnAll()
    {
        StopAllCoroutines();
        _zoneExitCoroutine = null;

        foreach (var instance in new List<GameObject>(_allActive))
        {
            if (instance == null) continue;
            var enemy = instance.GetComponent<Enemy>();
            if (enemy != null)
            {
                if (_spawnDeathHandlers.TryGetValue(instance, out var dh))
                    enemy.Health.OnDeath -= dh;
                deathNotifier?.Unregister(enemy.Health);
                if (enemy is ITimeAffected ta) timeFactorBootstrapper?.UnregisterEntity(ta);
                instance.GetComponent<EnemySocialBond>()?.ClearBond();
            }
            if (_pool != null && _activePrefabMap.TryGetValue(instance, out var prefab))
                _pool.Return(prefab, instance);
        }

        _allActive.Clear();
        _activePrefabMap.Clear();
        _instanceZoneMap.Clear();
        _spawnDeathHandlers.Clear();
        _pendingBondLeft.Clear(); _pendingBondRight.Clear();
        _pendingSeveredLeft.Clear(); _pendingSeveredRight.Clear();
        _activeCountsLeft.Clear(); _activeCountsRight.Clear();
        _activeLeft = _activeRight = 0;
        _activeGroupCount = 0;
        _activeZone = null;
        _activeConfig = null;

        Debug.Log("[EnemySpawner] DespawnAll complete.");
    }

    public void DespawnZone(SpawnZone zone)
    {
        if (zone == null) return;
        var toReturn = (from kv in _instanceZoneMap where kv.Value == zone select kv.Key).ToList();
        if (toReturn.Count == 0) return;

        foreach (var instance in toReturn)
        {
            if (instance == null) { _instanceZoneMap.Remove(instance); continue; }
            var enemy = instance.GetComponent<Enemy>();
            if (enemy != null)
            {
                if (_spawnDeathHandlers.TryGetValue(instance, out var dh))
                    enemy.Health.OnDeath -= dh;
                deathNotifier?.Unregister(enemy.Health);
                if (enemy is ITimeAffected ta) timeFactorBootstrapper?.UnregisterEntity(ta);
                instance.GetComponent<EnemySocialBond>()?.ClearBond();
            }
            if (_pool != null && _activePrefabMap.TryGetValue(instance, out var prefab))
                _pool.Return(prefab, instance);

            _allActive.Remove(instance);
            _activePrefabMap.Remove(instance);
            _instanceZoneMap.Remove(instance);
            _spawnDeathHandlers.Remove(instance);
        }
        Debug.Log($"[EnemySpawner] DespawnZone: returned {toReturn.Count} enemies from '{zone.name}'.");
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
        var bp = POIManager.Instance?.GetNearest(pos, POIType.Barrier);
        if (bp == null) return SpawnSide.Left;
        return pos.x < bp.transform.position.x ? SpawnSide.Left : SpawnSide.Right;
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

        // Scatter in a short ring around the point — enemies materialising exactly ON the spawn-point
        // origin mismatched the spawn VFX (BUG-070). NavMesh-sampled so the offset never lands off-mesh;
        // falls back to the exact point if the offset misses the mesh.
        Vector2 ring = UnityEngine.Random.insideUnitCircle.normalized
                       * UnityEngine.Random.Range(_spawnScatterRadius * 0.4f, _spawnScatterRadius);
        Vector3 scattered = point.position + new Vector3(ring.x, 0f, ring.y);
        if (NavMesh.SamplePosition(scattered, out NavMeshHit sHit, 2f, NavMesh.AllAreas))
            return sHit.position;
        return NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position : Vector3.zero;
    }
}