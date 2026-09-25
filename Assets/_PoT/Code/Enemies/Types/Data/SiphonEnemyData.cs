using UnityEngine;

[CreateAssetMenu(fileName = "SiphonEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Siphon")]
public class SiphonEnemyData : RangedEnemyData
{
    [Header("Siphon — Kite behaviour")]
    [Tooltip("Distance Siphon tries to maintain from player.")]
    public float preferredRange = 8f;
    [Tooltip("Distance at which Siphon panics and drops bomb + retreats.")]
    public float panicRange = 2.5f;
    [Tooltip("Speed multiplier during retreat.")]
    public float retreatSpeedMultiplier = 1.35f;

    [Header("Panic Bomb")]
    public float panicBombWindUpDuration = 0.25f;
    public float panicBombTravelDuration = 1.0f;
    public float panicBombDetonationDelay = 0.75f;
    public float panicBombAoeRadius = 1.5f;

    [Header("Ghost")]
    public float ghostTriggerRadius = 9f;
    public float ghostSpeedMultiplier = 1.4f;
    public float ghostKillWindowDuration = 2.2f;
    public float ghostBindDuration = 2f;
    public float ghostRetryDelay = 0.75f;
    public float ghostStunOnBreakDuration = 1.25f;
    public int ghostMashThreshold = 8;
    public float ghostMaxHp = 8f;

    [Header("Ghost chain throw (binds the soul — Mizuki-style)")]
    [Tooltip("The ghost stops pursuing and throws its binding chain at the soul within this distance. Keep it " +
             "SMALL — the chain must only ever catch the soul, at close range.")]
    public float ghostThrowRange = 2.5f;
    [Tooltip("Telegraph before the chain throws — the marker reveals 0→1 over it (the soul's dodge window).")]
    public float ghostMarkerWindup = 0.4f;
    [Tooltip("Seconds the thrown chain takes to reach the soul's position.")]
    public float ghostChainTravelTime = 0.4f;
    [Tooltip("Catch radius at the chain's landing point (proximity to the soul).")]
    public float ghostChainHitRadius = 0.6f;
}