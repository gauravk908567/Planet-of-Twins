using UnityEngine;

[CreateAssetMenu(fileName = "SeveredEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Severed")]
public class SeveredEnemyData : EnemyData
{
    [Header("Linked Damage Split")]
    [Tooltip("Fraction of damage applied to self. Partner gets (1 - directFraction).")]
    public float directDamageFraction = 0.75f;

    [Header("Grace Window")]
    [Tooltip("Seconds after partner death before Grief Rage triggers.")]
    public float graceWindowDuration = 1.2f;

    [Header("Grief Rage")]
    public float rageDuration = 8f;
    [Tooltip("Attack speed multiplier during rage.")]
    public float rageAttackSpeedMultiplier = 1.45f;
    [Tooltip("Attack cooldown multiplier during rage (< 1 = faster).")]
    public float rageCooldownMultiplier = 0.4f;

    [Header("Reach Animation")]
    [Tooltip("How often the Severed performs the reach animation toward the barrier.")]
    public float reachInterval = 4f;
    [Tooltip("Duration of the reach animation vulnerability window.")]
    public float reachDuration = 1.2f;
}