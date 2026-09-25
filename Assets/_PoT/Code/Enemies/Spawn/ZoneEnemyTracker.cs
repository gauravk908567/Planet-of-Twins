using System.Collections;
using UnityEngine;

public class ZoneEnemyTracker : MonoBehaviour
{
    // Set by EnemySpawner at spawn time
    public SpawnZone HomeZone { get; set; }

    private Enemy _enemy;
    private bool _returningHome;

    private void Awake() => _enemy = GetComponent<Enemy>();

    // Called by EnemyChaseState when player sight is lost
    public void OnPlayerSightLost()
    {
        if (HomeZone == null || _returningHome) return;

        // Check if enemy has followed player into a different zone
        if (!IsInsideHomeZone())
            StartCoroutine(ReturnToHomeAndDespawn());
    }

    private bool IsInsideHomeZone()
    {
        if (HomeZone == null) return true;
        var col = HomeZone.GetComponent<Collider>();
        if (col == null) return true;
        return col.bounds.Contains(transform.position);
    }

    private IEnumerator ReturnToHomeAndDespawn()
    {
        _returningHome = true;

        // Freeze state machine — no longer chasing
        //_enemy.StateMachine.Pause();
        _enemy.Movement.OnFreeze();

        // Walk back to home zone centre using NavMesh
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.SetDestination(HomeZone.transform.position);

            // Wait until reached or 8 seconds timeout
            float timeout = 8f;
            while (timeout > 0f &&
                   agent.pathPending ||
                   agent.remainingDistance > 1.5f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        // Arrived or timed out — despawn via pool
        _enemy.Health.TakeDamage(
            new DamageData(99999f, DamageType.Environmental));
    }

    public void ResetForPool()
    {
        HomeZone = null;
        _returningHome = false;
    }
}
