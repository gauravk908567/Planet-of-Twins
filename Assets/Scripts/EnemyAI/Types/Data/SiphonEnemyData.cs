using UnityEngine;

[CreateAssetMenu(fileName = "SiphonEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Siphon")]
public class SiphonEnemyData : EnemyData
{
    [Header("Siphon � Kite behaviour")]
    [Tooltip("Distance Siphon tries to maintain from player.")]
    public float preferredRange = 8f;
    [Tooltip("Distance at which Siphon panics and drops bomb + retreats.")]
    public float panicRange = 2.5f;
    [Tooltip("Speed multiplier during retreat.")]
    public float retreatSpeedMultiplier = 1.35f;

    [Header("Panic Bomb")]
    [Tooltip("Seconds Siphon stops to wind up before rolling bomb.")]
    public float panicBombWindUpDuration = 0.25f;
    [Tooltip("Seconds the bomb rolls before exploding.")]
    public float panicBombTravelDuration = 1.0f;
    [Tooltip("Seconds the bomb sits after rolling before exploding.")]
    public float panicBombDetonationDelay = 0.75f;
    [Tooltip("Explosion radius of the panic bomb.")]
    public float panicBombAoeRadius = 1.5f;

    [Header("Ghost")]
    [Tooltip("Radius around either twin within which a Ghost spawns on rescue.")]
    public float ghostTriggerRadius = 9f;
    [Tooltip("How fast the Ghost pursues the soul. Multiplier of soul speed.")]
    public float ghostSpeedMultiplier = 1.4f;
    [Tooltip("How long after spawning the soul can kill the ghost before it becomes immune.")]
    public float ghostKillWindowDuration = 2.2f;
    public float ghostBindDuration = 2f;
    public float ghostRetryDelay = 0.75f;
    [Tooltip("How long ghost backs away after soul breaks the chain.")]
    public float ghostStunOnBreakDuration = 1.25f;
    public int ghostMashThreshold = 8;
    [Tooltip("HP of the ghost � 2-3 soul E hits should kill it.")]
    public float ghostMaxHp = 8f;
}