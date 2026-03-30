using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class RadiantSeekerOrb : MonoBehaviour
{
    private NavMeshAgent _agent;
    private LayerMask _enemyLayer;
    private float _possessionRadius;
    private float _possessionDuration;
    private Transform _target;
    private bool _detonated;
    private bool _initialised;
    private RadiantSeekerAbility _owner;

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

        if (!_agent.pathPending && _agent.hasPath
            && _agent.remainingDistance <= _agent.stoppingDistance)
            Detonate();
    }

    private void OnDestroy()
    {
        _owner?.NotifyOrbDestroyed();
    }

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
            Destroy(gameObject); // no enemies left — destroy
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

        Debug.Log($"[RadiantSeekerOrb] Detonated — possessed {hits.Length} enemies");

        // Destroy immediately — cooldown already started at cast time
        Destroy(gameObject);
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