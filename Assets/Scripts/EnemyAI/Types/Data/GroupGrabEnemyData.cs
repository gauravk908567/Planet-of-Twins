using UnityEngine;

[CreateAssetMenu(fileName = "GroupGrabEnemyData", menuName = "PlanetOfTwins/Enemy Data/GroupGrab")]
public class GroupGrabEnemyData : EnemyData
{
    [Header("Behind Detection")]
    [Tooltip("Seconds enemy must stay behind player before grab triggers.")]
    public float behindTimeRequired = 1.5f;
    [Tooltip("Dot product threshold — negative = behind player. -0.3 = generous behind arc.")]
    public float behindDotThreshold = -0.3f;

    [Header("Grab — Cooldown")]
    [Tooltip("Seconds before enemy can grab again after rescue.")]
    public float grabCooldownAfterRescue = 3f;

    [Header("Alert Ranges")]
    [Tooltip("Alert range while chasing — nearby enemies join chase.")]
    public float chaseAlertRange = 6f;
    [Tooltip("Alert range after grabbing — much larger, pulls in pile.")]
    public float grabAlertRange = 14f;
    [Tooltip("Radius within which allies join the pile after grab.")]
    public float pileRadius = 4f;

    [Header("Struggle")]
    [Tooltip("Seconds TTK pauses when grabbed player struggles.")]
    public float strugglePauseDuration = 0.4f;

    [Header("Trap Tier")]
    [Tooltip("1 = player can struggle. 2 = fully frozen.")]
    public int trapTier = 2;
}