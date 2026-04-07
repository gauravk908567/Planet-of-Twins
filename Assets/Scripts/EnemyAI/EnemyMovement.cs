using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour, ITimeAffected
{
    private NavMeshAgent _agent;

    public NavMeshAgent Agent => _agent; // kept for EnemyAttackState.Enter — see note

    [SerializeField] private float moveSpeed = 3.5f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(targetPosition);
    }

    public void Stop()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    // ITimeAffected
    public void OnEffectStarted() => OnFreeze();
    public void OnEffectEnded() => OnUnfreeze();

    public void OnFreeze()
    {
        if (_agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = true;
    }

    public void OnUnfreeze()
    {
        if (_agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;
    }

    public void SetSpeed(float speed)
    {
        moveSpeed = speed;
        // _agent may not be assigned yet if called before Awake() runs
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_agent != null) _agent.speed = speed;
    }
}
