using UnityEngine;

[CreateAssetMenu(fileName = "RangedEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Ranged")]
public class RangedEnemyData : EnemyData
{
    [Header("Ranged — Engage Distance")]
    public float minEngageRange = 3f;
    public float desiredRange = 8f;

    // Projectile config (useProjectile / projectilePrefab / projectileSpeed) lives on the EnemyData base
    // now — any enemy, melee included, fires when a prefab is assigned there. Existing ranged assets keep
    // their serialized values (field names unchanged). useProjectile=false on a ranged archetype = raycast.
}