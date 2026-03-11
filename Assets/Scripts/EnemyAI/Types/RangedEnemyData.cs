using UnityEngine;

[CreateAssetMenu(fileName = "RangedEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Ranged")]
public class RangedEnemyData : EnemyData
{
    [Header("Ranged — Engage Distance")]
    public float minEngageRange = 3f;
    public float desiredRange = 8f;

    [Header("Ranged — Attack Mode")]
    [Tooltip("True = spawns projectile. False = instant raycast.")]
    public bool useProjectile = true;

    [Header("Ranged — Projectile (useProjectile = true only)")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 14f;
}