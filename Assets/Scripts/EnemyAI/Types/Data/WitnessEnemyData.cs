using UnityEngine;

[CreateAssetMenu(fileName = "WitnessEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Witness")]
public class WitnessEnemyData : EnemyData
{
    [Header("Shadow Positioning")]
    [Tooltip("Distance Witness maintains behind its ally (W → Ally → Twin formation).")]
    public float shadowDistance = 2.5f;

    [Header("Buff Aura")]
    [Tooltip("Radius within which nearby enemies receive the buff.")]
    public float buffAuraRadius = 6f;
    [Tooltip("Damage multiplier applied to buffed enemies. 1.4 = +40%.")]
    public float buffDamageMultiplier = 1.4f;
    [Tooltip("Attack cooldown multiplier applied to buffed enemies. 0.5 = half cooldown.")]
    public float buffCooldownMultiplier = 0.5f;

    [Header("Bomb")]
    [Tooltip("Distance at which Witness throws bomb at approaching twin.")]
    public float bombTriggerRange = 5f;
    [Tooltip("Seconds before bomb throw — ability can interrupt during this window.")]
    public float bombWindUpDuration = 0.75f;
    [Tooltip("Seconds the bomb sits before exploding.")]
    public float bombDetonationDelay = 0.75f;
    [Tooltip("AOE radius of the bomb explosion.")]
    public float bombAoeRadius = 1.5f;

    [Header("Summoning Ritual")]
    [Tooltip("Seconds Witness channels to summon a new melee ally.")]
    public float ritualDuration = 4f;
    [Tooltip("Distance at which player proximity interrupts the ritual.")]
    public float ritualInterruptRange = 5f;
    [Tooltip("Safe distance Witness flees to before restarting ritual.")]
    public float ritualFleeDistance = 10f;
    [Tooltip("Speed multiplier while fleeing after ritual interrupt.")]
    public float fleeSpeedMultiplier = 1.6f;
    [Tooltip("Seconds after spawning ally before Witness can summon again.")]
    public float summonCooldown = 2f;
}