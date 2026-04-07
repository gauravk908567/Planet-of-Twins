using UnityEngine;

[CreateAssetMenu(fileName = "TetherBreakerEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/TetherBreaker")]
public class TetherBreakerEnemyData : EnemyData
{
    [Header("Chain Attack")]
    [Tooltip("Distance at which Tether-Breaker locks position and throws chain.")]
    public float chainAttackRange = 8f;
    [Tooltip("Seconds the chain takes to travel to target.")]
    public float chainTravelTime = 1.25f;
    [Tooltip("Hit radius of the chain at landing.")]
    public float chainHitRadius = 0.55f;
    [Tooltip("E presses needed to break the chain.")]
    public int chainMashThreshold = 8;
    [Tooltip("Seconds after miss before re-engaging (pull-back animation window).")]
    public float chainMissCooldown = 1.8f;
    [Tooltip("Seconds after chain connects before Tether-Breaker starts sprinting.")]
    public float chainConnectDelay = 0.15f;

    [Header("Sprint (chain connected)")]
    [Tooltip("Speed multiplier when sprinting away with chain connected.")]
    public float sprintSpeedMultiplier = 2.2f;

    [Header("Rage (chain broken)")]
    [Tooltip("Speed multiplier during rage chase.")]
    public float rageSpeedMultiplier = 1.8f;
}