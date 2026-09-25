using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "PlanetOfTwins/Enemy Data/Base")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Enemy";

    [Header("Health")]
    public float maxHealth = 40f;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float wanderRadius = 3f;
    public float wanderTimerMin = 5f;
    public float wanderTimerMax = 12f;

    [Header("Social Bond")]
    [Tooltip("Grace window before dying with bond partner. Severed=1.2, BasicMelee=0.8 etc.")]
    public float deathBondGraceDuration = 1.2f;

    [Tooltip("Knockback force multiplier. 1 = full force, 0 = immune, 0.5 = half push.")]
    public float knockbackForceMultiplier = 1f;

    [Header("Detection")]
    public float detectionRange = 8f;
    public float possessedDetectionMultiplier = 4f;

    [Header("Attack")]
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    public float attackWindup = 0.3f;

    [Header("Attack — Projectile (optional, any enemy)")]
    [Tooltip("True + prefab assigned = this enemy's basic attack fires the projectile at its current target " +
             "instead of the melee overlap. Possession and clan-war attacks stay melee. The prefab needs an " +
             "Arrow component and a trigger collider; it spawns through GameplayPool. Leave off for pure melee.")]
    public bool useProjectile = false;
    public GameObject projectilePrefab;
    public float projectileSpeed = 14f;

    [Header("Possession Recovery")]
    public float returnAnimDuration = 1.5f;

    [Header("Accord Spirits")]
    [Tooltip("If true this enemy can damage and destroy Accord Spirit entities.")]
    public bool canDamageSpirits = false;
    [Tooltip("Damage per hit to spirits. Only used if canDamageSpirits = true.")]
    public float spiritDamage = 25f;

    [Header("Possession")]
    public bool canBePossessed = true;
    public float possessionDuration = 5f;

    [Header("Killing Blow TTK — any enemy that can down a player")]
    public float timeToKill = 8f;
    public float mashFrequency = 4f;
    public float mashWindowDuration = 3f;
    public float mashCooldown = 0.75f;
    public float partialHealAmount = 20f;

    [Header("Clan War")]
    [Tooltip("If either twin is within this range, TwinInDangerRange = true on the per-enemy blackboard. " +
             "GOAPGoalAttackEnemy hard-gates off this flag — so the enemy abandons clan combat and " +
             "switches priority to attacking the twin. Typical range: slightly larger than detectionRange " +
             "so disengagement happens before full aggro. Set to 0 to disable threat switching — " +
             "enemy stays locked on clan combat regardless of twin proximity (useful for scripted scenes).")]
    public float twinThreatRange = 10f;

    [Tooltip("Damage multiplier when attacking another Enemy during clan war. " +
         "0.3 = 30% damage. Possession overrides — possessed attackers deal full damage.")]
    [Range(0.1f, 1f)]
    public float clanWarDamageMultiplier = 0.3f;
}