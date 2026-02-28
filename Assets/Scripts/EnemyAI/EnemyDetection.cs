using UnityEngine;

public class EnemyDetection : MonoBehaviour
{
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask enemyLayer;

    public Transform DetectTarget(Faction myFaction, bool isPossessed = false)
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            isPossessed ? detectionRange * 4 : detectionRange,
            myFaction == Faction.Enemy ? playerLayer : enemyLayer
        );

        foreach (var hit in hits)
        {
            var faction = hit.GetComponent<FactionComponent>();
            if (faction == null)
                continue;
            if (myFaction == Faction.PossessedEnemy)
            {
                if (faction.CurrentFaction == Faction.Enemy || faction.CurrentFaction == Faction.PossessedEnemy)
                {
                    return hit.transform;
                }
                else
                {
                    return transform;
                }
            }
            else if (faction.CurrentFaction != myFaction)
            {
                return hit.transform;
            }
        }

        return null;
    }

    public bool IsPlayerInRange(float range, Transform player)
    {
        if (player == null) return false;

        return Vector3.Distance(transform.position, player.position) <= range;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}