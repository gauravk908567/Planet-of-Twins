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

    [Header("Killing Blow TTK � any enemy that can down a player")]
    public float timeToKill = 8f;
    public float mashFrequency = 4f;
    public float mashWindowDuration = 3f;
    public float mashCooldown = 0.75f;
    public float partialHealAmount = 20f;
}