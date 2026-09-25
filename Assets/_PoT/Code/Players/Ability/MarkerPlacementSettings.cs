using UnityEngine;

[CreateAssetMenu(fileName = "MarkerPlacementSettings",
                 menuName = "PlanetOfTwins/Marker Placement Settings")]
public class MarkerPlacementSettings : ScriptableObject
{
    [Tooltip("Maximum distance from the central barrier the marker can be placed. " +
             "Skill tree upgrades increase this.")]
    public float MaxDistanceFromBarrier = 4f;

    [Tooltip("Minimum fallback distance if max is obstructed.")]
    public float MinDistanceFromBarrier = 2f;

    [Tooltip("Radius used to check if a candidate position is inside an obstacle.")]
    public float ObstacleCheckRadius = 0.4f;

    [Tooltip("Steps between max and min to check for obstacles. Higher = smoother fallback.")]
    public int FallbackSteps = 5;
}