using UnityEngine;

/// <summary>
/// ScriptableObject defining what a bomb does on detonation.
/// Each enemy type has its own BombEffectData SO — same BombProjectile script,
/// different data. Designer configures all values per enemy in Inspector.
///
/// CREATE: Right-click Assets → PlanetOfTwins/Bomb Effect Data
/// </summary>
[CreateAssetMenu(fileName = "BombEffectData",
                 menuName = "PlanetOfTwins/Bomb Effect Data")]
public class BombEffectData : ScriptableObject
{
    [Header("Damage")]
    [Tooltip("Damage dealt on detonation. 0 = no direct damage.")]
    public float damage = 0f;
    public DamageType damageType = DamageType.Ability;

    [Header("Ability Suppression")]
    [Tooltip("If true, all abilities are suppressed on hit twins.")]
    public bool suppressAllAbilities = false;
    [Tooltip("If true, only primary Q ability is suppressed on hit twins.")]
    public bool suppressPrimaryOnly = true;
    [Tooltip("How long the suppression lasts in seconds.")]
    public float suppressionDuration = 1.5f;

    [Header("Movement Slow")]
    [Tooltip("Speed multiplier applied to hit twins. 0.65 = -35% speed.")]
    public float slowMultiplier = 0.65f;
    [Tooltip("How long the slow lasts in seconds.")]
    public float slowDuration = 1.5f;

    [Header("Pushback")]
    [Tooltip("Force applied outward from explosion centre.")]
    public float pushbackForce = 5f;

    [Header("Enemy Effects")]
    [Tooltip("If true, bomb also affects enemies caught in radius (same effects).")]
    public bool affectsEnemies = true;
}