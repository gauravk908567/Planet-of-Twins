using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Categorized pool buckets — one root child per category under GameplayPoolRoot.</summary>
public enum PoolCategory { Projectiles, AbilityObjects, Summons, Hazards }

/// <summary>
/// Reset contract for pooled GAMEPLAY objects (P16). Implemented by the pooled
/// prefab's components; the pool calls these on every component in the instance's hierarchy.
/// OnDespawned rules: stop owned CueHandles · reset state fields · unsubscribe named handlers ·
/// clear event fields (stale subscribers). The pool itself already: StopAllCoroutines on every
/// MonoBehaviour, FxManager.StopAllOn(transform) (safety net), reparents to the category root.
/// </summary>
public interface ISpawnPoolable
{
    void OnSpawned(GameplayPool pool);
    void OnDespawned();
}

/// <summary>
/// The one Persistent pool for GAMEPLAY spawns (P16): projectiles, ability
/// objects, summons, hazards. Cosmetics stay in FxManager's VfxPool; enemies stay in EnemyPool
/// (both already correct). Instances are reused, never destroyed at runtime, and reparented back
/// under GameplayPoolRoot/<Category> on Return — an area unload or a despawning host can never
/// take a pooled instance with it (F1 class).
///
/// Usage (call sites use the STATICS — they LogError + degrade to Instantiate/Destroy when the
/// Persistent GO is missing, so direct-play stays alive while the mistake is loud):
///   var go = GameplayPool.Spawn(prefab, PoolCategory.Projectiles, pos, rot);
///   GameplayPool.Despawn(go);                  // instead of Destroy(go)
///   GameplayPool.Despawn(go, 2f);              // instead of Destroy(go, 2f) — version-stamped:
///                                              //   a stale timer can never kill a reused instance.
/// Cue-on-spawn stays CALLER-played through the book (pool owns lifetime, caller owns presentation).
/// </summary>
public class GameplayPool : MonoBehaviour
{
    public static GameplayPool Instance { get; private set; }

    [Tooltip("Optional prewarm rows {prefab, count, category} — instantiated inactive in Start.")]
    [SerializeField] private SpawnPrewarmProfile _prewarmProfile;

    private sealed class InstanceInfo
    {
        public GameObject Prefab;
        public PoolCategory Category;
        public bool InPool;
        public int Version;   // bumped on every Get — stale delayed-despawns compare against it
    }

    private readonly Dictionary<GameObject, Queue<GameObject>> _free = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, InstanceInfo> _instances = new Dictionary<GameObject, InstanceInfo>();
    private Transform[] _categoryRoots;

    // ── Refcounted per-prefab warm pools ──────────────────────────────────────
    // A consumer (an enemy whose data carries a projectile, a bomb thrower…) registers the prefab it
    // will spawn; the pool warms N instances and keeps them while ANY user is alive. When the last
    // user despawns, the prefab's free instances are destroyed after a grace period — pools follow
    // the enemies on screen instead of accumulating forever, and the prefab reference stays the ONE
    // the consumer already holds (no duplicate prewarm-profile row to keep in sync).
    private sealed class PrefabUsers { public int Count; public Coroutine Trim; }
    private readonly Dictionary<GameObject, PrefabUsers> _users = new Dictionary<GameObject, PrefabUsers>();
    private const float TrimGraceSeconds = 10f;   // scaled — a respawn wave inside the grace reuses the pool

    // ── statics (the call-site API — fail-loud degrade, never a silent null) ──
    public static GameObject Spawn(GameObject prefab, PoolCategory category, Vector3 pos, Quaternion rot)
    {
        if (Instance != null) return Instance.Get(prefab, category, pos, rot);
        Debug.LogError("[GameplayPool] No Instance — is the GameplayPool GO in Persistent? Falling back to Instantiate (unpooled).");
        return prefab != null ? Instantiate(prefab, pos, rot) : null;
    }

    public static void Despawn(GameObject instance)
    {
        if (instance == null) return;
        if (Instance != null && Instance._instances.ContainsKey(instance)) { Instance.Return(instance); return; }
        Destroy(instance);   // not pool-owned (fallback-spawned or foreign) — plain destroy
    }

    public static void Despawn(GameObject instance, float delay)
    {
        if (instance == null) return;
        if (Instance != null && Instance._instances.ContainsKey(instance)) { Instance.ReturnAfter(instance, delay); return; }
        Destroy(instance, delay);
    }

    /// <summary>A live consumer of this prefab appeared — warm the pool and hold it open. Pair with
    /// <see cref="RemoveUser"/>; enemies should go through <c>Enemy.RegisterPooledPrefab</c>, which
    /// pairs the release automatically on despawn.</summary>
    public static void AddUser(GameObject prefab, PoolCategory category, int warmCount = 2)
        => Instance?.AddUserInternal(prefab, category, warmCount);

    /// <summary>The consumer despawned — when the last one goes, free instances are destroyed after a grace.</summary>
    public static void RemoveUser(GameObject prefab)
        => Instance?.RemoveUserInternal(prefab);

    // ── lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var categories = (PoolCategory[])System.Enum.GetValues(typeof(PoolCategory));
        _categoryRoots = new Transform[categories.Length];
        foreach (var cat in categories)
        {
            var root = new GameObject(cat.ToString()).transform;
            root.SetParent(transform, false);
            _categoryRoots[(int)cat] = root;
        }
    }

    private void Start()
    {
        if (_prewarmProfile == null) return;
        foreach (var row in _prewarmProfile.rows)
        {
            if (row.prefab == null || row.count <= 0) continue;
            for (int i = 0; i < row.count; i++)
            {
                var inst = CreateInstance(row.prefab, row.category);
                _free[row.prefab].Enqueue(inst);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── instance API ──────────────────────────────────────────────────────────
    public GameObject Get(GameObject prefab, PoolCategory category, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;

        if (!_free.TryGetValue(prefab, out var q)) { q = new Queue<GameObject>(); _free[prefab] = q; }
        GameObject inst = null;
        while (q.Count > 0 && inst == null) inst = q.Dequeue();   // skip externally-destroyed entries
        if (inst == null) inst = CreateInstance(prefab, category);

        var info = _instances[inst];
        info.InPool = false;
        info.Version++;

        inst.transform.SetParent(_categoryRoots[(int)category], false);
        inst.transform.SetPositionAndRotation(pos, rot);
        inst.SetActive(true);

        // NavMesh users start ON the mesh at the spawn point — only if already enabled;
        // agents the object manages itself (orb/spirit re-enable + Warp in Initialise) are left alone.
        var agent = inst.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled) agent.Warp(pos);

        foreach (var p in inst.GetComponentsInChildren<ISpawnPoolable>(true))
            p.OnSpawned(this);

        return inst;
    }

    public void Return(GameObject instance)
    {
        if (instance == null) return;
        if (!_instances.TryGetValue(instance, out var info))
        {
            Debug.LogWarning($"[GameplayPool] Return('{instance.name}') — not pool-owned; destroying.", instance);
            Destroy(instance);
            return;
        }
        if (info.InPool) return;   // double-return guard (hit + lifetime timer racing is normal)

        // One faulty component must never abort the return halfway — an aborted Return left live
        // "stuck" instances (active, half-reset, InPool=false) that then flew again with stale
        // state (BUG-056). Log the offender, keep returning.
        foreach (var p in instance.GetComponentsInChildren<ISpawnPoolable>(true))
            try { p.OnDespawned(); }
            catch (System.Exception ex) { Debug.LogException(ex, instance); }

        // Safety nets (the OnDespawned contract should already have done the precise version):
        foreach (var mb in instance.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) mb.StopAllCoroutines();
        try { FxManager.Instance?.StopAllOn(instance.transform); }
        catch (System.Exception ex) { Debug.LogException(ex, instance); }

        instance.SetActive(false);
        instance.transform.SetParent(_categoryRoots[(int)info.Category], false);
        info.InPool = true;

        // A refcounted prefab whose last user is gone and grace already expired: destroy instead of
        // re-enqueueing so in-flight stragglers can't outlive the trim. Untracked prefabs pool as before.
        if (_users.TryGetValue(info.Prefab, out var u) && u.Count == 0 && u.Trim == null)
        {
            _instances.Remove(instance);
            Destroy(instance);
            return;
        }
        _free[info.Prefab].Enqueue(instance);
    }

    /// <summary>Version-stamped delayed return — if the instance is returned AND re-spawned while
    /// the timer runs, the stale timer is a no-op (never kills the new use).</summary>
    public void ReturnAfter(GameObject instance, float delay)
    {
        if (instance == null) return;
        if (!_instances.TryGetValue(instance, out var info)) { Destroy(instance, delay); return; }
        StartCoroutine(ReturnAfterRoutine(instance, info, info.Version, delay));
    }

    private IEnumerator ReturnAfterRoutine(GameObject instance, InstanceInfo info, int version, float delay)
    {
        yield return new WaitForSeconds(delay);   // scaled — gameplay lifetimes slow under Setsuna (R10)
        if (instance == null || info.InPool || info.Version != version) yield break;
        Return(instance);
    }

    private void AddUserInternal(GameObject prefab, PoolCategory category, int warmCount)
    {
        if (prefab == null) return;
        if (!_users.TryGetValue(prefab, out var u)) { u = new PrefabUsers(); _users[prefab] = u; }
        u.Count++;
        if (u.Trim != null) { StopCoroutine(u.Trim); u.Trim = null; }

        if (!_free.TryGetValue(prefab, out var q)) { q = new Queue<GameObject>(); _free[prefab] = q; }
        while (q.Count < warmCount)
            q.Enqueue(CreateInstance(prefab, category));
    }

    private void RemoveUserInternal(GameObject prefab)
    {
        if (prefab == null || !_users.TryGetValue(prefab, out var u)) return;
        u.Count = Mathf.Max(0, u.Count - 1);
        if (u.Count == 0 && u.Trim == null)
            u.Trim = StartCoroutine(TrimAfterGrace(prefab, u));
    }

    private IEnumerator TrimAfterGrace(GameObject prefab, PrefabUsers u)
    {
        yield return new WaitForSeconds(TrimGraceSeconds);
        u.Trim = null;
        if (u.Count > 0) yield break;
        // Destroy the FREE instances; in-flight ones are destroyed as they Return (see Return()).
        if (_free.TryGetValue(prefab, out var q))
            while (q.Count > 0)
            {
                var inst = q.Dequeue();
                if (inst != null) { _instances.Remove(inst); Destroy(inst); }
            }
    }

    private GameObject CreateInstance(GameObject prefab, PoolCategory category)
    {
        var inst = Instantiate(prefab, _categoryRoots[(int)category]);
        inst.SetActive(false);
        _instances[inst] = new InstanceInfo { Prefab = prefab, Category = category, InPool = true };
        if (!_free.ContainsKey(prefab)) _free[prefab] = new Queue<GameObject>();
        return inst;
    }
}
