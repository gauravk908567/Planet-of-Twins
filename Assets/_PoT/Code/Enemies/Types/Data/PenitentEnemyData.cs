using UnityEngine;

[CreateAssetMenu(fileName = "PenitentEnemyData",
                 menuName = "PlanetOfTwins/Enemy Data/Penitent")]
public class PenitentEnemyData : EnemyData
{
    [Header("Grab Cycle")]
    [Tooltip("Seconds Penitent stops and winds up before grabbing. Player can dodge during this.")]
    public float grabWindUpDuration = 1.25f;
    [Tooltip("Seconds the crush hold lasts before auto-release.")]
    public float crushDuration = 2f;
    [Tooltip("Seconds Penitent stands still after releasing before resuming chase.")]
    public float postGrabCooldown = 1.25f;
    [Tooltip("Distance at which grab triggers.")]
    public float crushRange = 1.5f;

    [Header("Crush Tick Damage")]
    [Tooltip("Total damage per second during normal grab — divided into ticks.")]
    public float crushDps = 15f;
    [Tooltip("Seconds between each damage tick during normal grab. 0.25 = 4 ticks/sec.")]
    public float crushTickInterval = 0.25f;
    [Tooltip("Total damage per second during rage — divided into ticks.")]
    public float rageCrushDps = 25f;
    [Tooltip("Seconds between each damage tick during rage. Faster = more threatening.")]
    public float rageTickInterval = 0.15f;
    [Tooltip("How long rage lasts after a reflection threshold is hit mid-grab.")]
    public float rageDuration = 4f;

    [Header("Self-Rescue (E mash)")]
    [Tooltip("E presses needed to break free from crush.")]
    public int crushMashThreshold = 8;

    [Header("Reflection Phases")]
    [Tooltip("HP thresholds that trigger Reflection Phase (as fraction of max HP).")]
    public float[] reflectionThresholds = { 0.7f, 0.4f, 0.1f };
    [Tooltip("Duration of each Reflection Phase in seconds.")]
    public float reflectionDuration = 2.2f;
    [Tooltip("Fraction of incoming damage reflected back to the hitting twin.")]
    public float reflectionFraction = 0.6f;
    [Tooltip("Speed multiplier during Reflection Phase / Rage.")]
    public float reflectionSpeedMultiplier = 1.4f;
}