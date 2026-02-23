using UnityEngine;

public class EnemyDetection : MonoBehaviour
{
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private LayerMask playerLayer;

    public Transform DetectTarget(Faction myFaction)
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            detectionRange,
            playerLayer
        );

        foreach (var hit in hits)
        {
            var faction = hit.GetComponent<FactionComponent>();
            if (faction == null)
                continue;
            if (myFaction == Faction.PossessedEnemy)
            {
                if (faction.CurrentFaction != Faction.Player)
                {
                    return hit.transform;
                }
                else if (faction.CurrentFaction == Faction.Enemy)
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}