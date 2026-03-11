using UnityEngine;

[CreateAssetMenu(fileName = "GroupGrabEnemyData", menuName = "PlanetOfTwins/Enemy Data/GroupGrab")]
public class GroupGrabEnemyData : EnemyData
{
    [Header("Grab — Timing")]
    [Tooltip("How long enemy must be behind player before initiating grab")]
    public float behindTimeRequired = 1.5f;
}
