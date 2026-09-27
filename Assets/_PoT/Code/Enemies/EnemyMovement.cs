using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh movement for an enemy. Its freeze is driven ONLY by the owning <see cref="Enemy"/>'s pause owners
/// (<see cref="Enemy.PauseBrain"/>): it is deliberately not an ITimeAffected any more, so no second path (the
/// time-factor registry) can restart a stunned enemy's movement (BUG-142).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    private NavMeshAgent _agent;

    public NavMeshAgent Agent => _agent; // kept for EnemyAttackState.Enter � see note

    [SerializeField] private float moveSpeed = 3.5f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
    }

    private void OnEnable()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_agent != null && _agent.isOnNavMesh)
            _agent.isStopped = false;
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
