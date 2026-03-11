using UnityEngine;

public class EnemyDetection : MonoBehaviour
{
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float possessedDetectionMultiplier = 4f; // was hardcoded *4
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask enemyLayer;

    public float DetectionRange => detectionRange;

    private Enemy _enemy;

    // Pre-allocated buffer — no Collider[] per frame
    private readonly Collider[] _hitBuffer = new Collider[10];

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    public void SetRanges(float detection, float possessedMultiplier)
    {
        detectionRange = detection;
        possessedDetectionMultiplier = possessedMultiplier;
    }
    public Transform DetectTarget(Faction myFaction, bool isPossessed = false)
    {
        float range = isPossessed
            ? detectionRange * possessedDetectionMultiplier
            : detectionRange;
        LayerMask layer = (myFaction == Faction.Enemy) ? playerLayer : enemyLayer;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, range, _hitBuffer, layer);

        Transform nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var faction = _hitBuffer[i].GetComponent<FactionComponent>();
            if (faction == null) continue;
            if (faction.CurrentFaction == myFaction) continue;

            float d = Vector3.Distance(transform.position, _hitBuffer[i].transform.position);
            if (d < nearestDist)
            {
                nearestDist = nearest == null ? d : nearestDist;
                nearest = _hitBuffer[i].transform;
                nearestDist = d;
            }
        }

        return nearest;
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