using UnityEngine;

public class Arrow : MonoBehaviour, IProjectileData, ISpawnPoolable
{
    private Vector3 _dir;
    private float _speed;
    private EnemyAttackController _controller;
    private bool _hasHit;

    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;
    [Tooltip("Optional anchor at the arrowhead — the head/trail cues follow it. Null = the root.")]
    [SerializeField] private Transform _tipAnchor;

    // Held flight cues (ArrowCueBook via the enemy library) — started on spawn, reclaimed on despawn.
    private CueHandle _trailHandle;
    private CueHandle _headHandle;

    // ── ISpawnPoolable (P16 — pooled via GameplayPool.Projectiles) ─────────────
    public void OnSpawned(GameplayPool pool)
    {
        // Defense-in-depth: state clean at ISSUE, not only at return. The stuck-arrow root cause
        // (BUG-056, solved 2026-07-11): a twin has MULTIPLE colliders, so one arrow gets TWO
        // OnTriggerEnter calls in the same physics pass — the first returned the arrow (resetting
        // _hasHit), the second re-set _hasHit=true and its despawn no-op'd on the InPool guard, so
        // the arrow entered the free queue with _hasHit=true and its NEXT use spawned frozen at the
        // muzzle (Update early-outs on _hasHit).
        _hasHit = false;

        var book = VfxLibraryProvider.Instance?.Enemy?.Arrow;
        var fx = FxManager.Instance;
        if (book == null || fx == null) return;   // fail-safe — arrow flies bare until the slot is authored
        var follow = _tipAnchor != null ? _tipAnchor : transform;
        _trailHandle = fx.PlayBook(book, FxIds.Enemy.Arrow.arrow_Trail, CueContext.Follow(follow));
        _headHandle  = fx.PlayBook(book, FxIds.Enemy.Arrow.arrow_Head,  CueContext.Follow(follow));
    }

    public void OnDespawned()
    {
        // State reset FIRST — if a cue Stop ever throws, the arrow must still come back clean
        // (a half-run OnDespawned left _hasHit=true on a live instance — BUG-056).
        _hasHit = false;
        _controller = null;
        _dir = Vector3.zero;
        var fx = FxManager.Instance;
        fx?.Stop(_trailHandle);  _trailHandle = CueHandle.None;
        fx?.Stop(_headHandle);   _headHandle = CueHandle.None;
    }

    // Called by EnemyAttackController.FireProjectile() � canonical path
    public void Initialise(Vector3 direction, float speed, EnemyAttackController controller)
    {
        _dir = direction;
        _speed = speed;
        _controller = controller;
        // Industry-standard projectile orientation: root +Z looks along the velocity; the visual
        // mesh is authored head-forward under the root (never rotate the root in the prefab —
        // GameplayPool.Spawn stamps it, which is why prefab-root rotation edits "did nothing").
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
        GameplayPool.Despawn(gameObject, lifetime);   // version-stamped — inert if the arrow hits first
    }

    // IProjectileData legacy path � controller is null, so OnTriggerEnter will
    // call _controller?.OnProjectileHit which is a no-op: the arrow hits but
    // deals ZERO damage silently. This is almost always a wiring mistake.
    public void Initialise(Vector3 direction, float speed)
    {
        Debug.LogError(
            $"[Arrow] Legacy Initialise called on {gameObject.name} � " +
            $"controller is null, hit will deal NO damage. " +
            $"Use Initialise(direction, speed, controller) instead.",
            this);
        Initialise(direction, speed, null);
    }

    private void Update()
    {
        if (_hasHit) return;
        transform.position += _dir * _speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        // Second trigger event in the SAME physics pass (twin has several colliders): the first one
        // already returned this arrow to the pool — the event on the now-inactive instance must be
        // inert, or it re-arms _hasHit inside the free queue AND deals double damage (BUG-056).
        if (!gameObject.activeInHierarchy) return;
        if (((1 << other.gameObject.layer) & hitLayers.value) == 0) return;

        _hasHit = true;

        // BUG-056 forensics: stuck arrows = this method started but the arrow never returned to the
        // pool, with no exception logged. The paired logs bracket the return — a "hit" without its
        // "returned" names the target AND the abort point on the next repro.
        Debug.Log($"[Arrow] hit '{other.name}' (root={other.transform.root.name}, layer={LayerMask.LayerToName(other.gameObject.layer)}) at {transform.position}", this);

        try
        {
            // Controller owns all damage logic — arrow is pure movement + collision reporter
            _controller?.OnProjectileHit(other);

            // Impact cue at the hit point (World — must not vanish with the pooled arrow).
            var book = VfxLibraryProvider.Instance?.Enemy?.Arrow;
            if (book != null)
                FxManager.Instance?.PlayBook(book, FxIds.Enemy.Arrow.arrow_OnImpact,
                    new CueContext(transform.position));
        }
        finally
        {
            // The arrow ALWAYS goes home — damage/cue trouble upstream must never strand it mid-air.
            GameplayPool.Despawn(gameObject);
            Debug.Log($"[Arrow] returned to pool (active={gameObject.activeSelf})", this);
        }
    }
}