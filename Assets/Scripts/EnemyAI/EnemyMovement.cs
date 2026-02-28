using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour, ITimeFreezable
{
    private NavMeshAgent agent;

    public NavMeshAgent Agent => agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        if (!agent.enabled) return;

        agent.SetDestination(targetPosition);
    }

    public void Stop()
    {
        if (!agent.enabled) return;

        agent.ResetPath();
    }

    public void OnFreeze()
    {
        agent.isStopped = true;
    }

    public void OnUnfreeze()
    {
        agent.isStopped = false;
    }
}