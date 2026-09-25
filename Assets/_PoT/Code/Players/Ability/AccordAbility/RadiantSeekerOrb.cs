using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class RadiantSeekerOrb : MonoBehaviour, ISpawnPoolable
{
    // ── ISpawnPoolable (P16 — pooled via GameplayPool.AbilityObjects) ──────────
    public void OnSpawned(GameplayPool pool) { }
    public void OnDespawned()
    {
        // Owner notification moved here from OnDestroy — pooled objects are never destroyed.
        _owner?.NotifyOrbDestroyed();
        _owner = null;
        _target = null;
        _detonated = false;
        _initialised = false;
        if (_agent != null) _agent.enabled = false;
    }

    private NavMeshAgent _agent;
    private LayerMask _enemyLayer;
    private float _possessionRadius;
    private float _possessionDuration;
    private Transform _target;
    private bool _detonated;
    private bool _initialised;
    private RadiantSeekerAbility _owner;

    // "radorb_hit" = detonation burst; "radorb_hiteffect" = per-enemy spark. Book from PlayerVfxLibrary (R4).
    private CueBookData _cueBook;

    public void Initialise(
        LayerMask enemyLayer,
        float possessionRadius = 2.5f,
        float possessionDuration = 2f,
        float reseekCooldown = 0f, // unused — kept for signature compat
        RadiantSeekerAbility owner = null)
    {
        _enemyLayer = enemyLayer;
        _possessionRadius = possessionRadius;
        _possessionDuration = possessionDuration;
        _owner = owner;
        _agent = GetComponent<NavMeshAgent>();
        _initialised = true;

        SeekNewTarget();
    }

    private void Update()
    {
        if (!_initialised || _detonated) return;

        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            SeekNewTarget();
            return;
        }

        _agent.SetDestination(_target.position);

        // Arrival: NavMesh arrival OR physically inside the possession radius. remainingDistance never
        // reaches stoppingDistance when the agents' avoidance keeps the orb out of the target's spot —
        // this orb's agent radius is 1.75, so it parks ~2.25 m away (radius sum) and stalls forever
        // (BUG-066). The floor term (own radius + target allowance) keeps arrival possible even if the
        // possession radius is ever tuned below the physical approach limit.
        float sqrToTarget = (_target.position - transform.position).sqrMagnitude;
        float arriveRadius = Mathf.Max(_possessionRadius * 0.95f, _agent.radius + 0.75f);
        if (sqrToTarget <= arriveRadius * arriveRadius
            || (!_agent.pathPending && _agent.hasPath
                && _agent.remainingDistance <= _agent.stoppingDistance))
            Detonate();
    }

    // OnDestroy removed (P16): the owner notify lives in OnDespawned — the pool always calls it.

    private void SeekNewTarget()
    {
        _target = FindNearestEnemy();
        if (_target != null)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);
            _agent.SetDestination(_target.position);
        }
        else
        {
            GameplayPool.Despawn(gameObject); // no enemies left
        }
    }

    private void Detonate()
    {
        if (_detonated) return;
        _detonated = true;
        _agent.enabled = false;

        Collider[] hits = Physics.OverlapSphere(
            transform.position, _possessionRadius, _enemyLayer);

        foreach (Collider hit in hits)
            hit.GetComponent<IPossessable>()?.ApplyPossession(_possessionDuration, 1.2f);

        // Detonation burst + per-enemy spark (World — pooled cues survive this orb's immediate Destroy below).
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.RadiantSeeker;   // R4
        if (_cueBook != null)
        {
            var fx = FxManager.Instance;
            fx?.PlayBook(_cueBook, FxIds.Player.RadiantSeeker.radorb_hit, new CueContext(transform.position));
            foreach (Collider hit in hits)
                fx?.PlayBook(_cueBook, FxIds.Player.RadiantSeeker.radorb_hiteffect,
                    new CueContext(hit.transform.position + Vector3.up * 0.5f));
        }

        Debug.Log($"[RadiantSeekerOrb] Detonated — possessed {hits.Length} enemies");

        // Despawn immediately — cooldown already started at cast time (pooled cues survive this)
        GameplayPool.Despawn(gameObject);
    }

    private Transform FindNearestEnemy()
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        float best = float.MaxValue;
        Transform result = null;

        foreach (Enemy e in all)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < best) { best = d; result = e.transform; }
        }

        return result;
    }
}