using UnityEngine;
using System;

public class TetherDistanceCalculator : MonoBehaviour, IDistanceProvider
{
    [SerializeField] private Transform leftTwin;
    [SerializeField] private Transform rightTwin;

 [Tooltip(": Weight applied to forward/backward (Z-axis) desync.\n" +
             "1.0 = equal to horizontal. 0.5 = recommended starting value (simulation verified).")]
    [SerializeField, Range(0f, 1f)] private float verticalDesyncWeight = 0.5f;

    [Tooltip("Unity world units per 1 game unit. The arena is measured in game units.\n" +
             "Set this so that 18 game units = the physical death-zone distance in your scene.\n" +
             "Replaces the unexplained * 0.2f magic number in original TwinBondManager.")]
    [SerializeField] private float worldUnitsPerGameUnit = 1f;

    public float GetDistance()
    {
        if (leftTwin == null || rightTwin == null) return 0f;

        Vector3 left = leftTwin.position;
        Vector3 right = rightTwin.position;

 // Horizontal separation (X-axis only — measures gap from each barrier bound)
        float horizontalGap = Mathf.Abs(left.x - right.x);

        // Forward/backward desync (Z-axis in Unity 3D)
        float verticalGap = Mathf.Abs(left.z - right.z);

        float rawDistance = horizontalGap + verticalDesyncWeight * verticalGap;

        //Debug.Log("Distance" + rawDistance/worldUnitsPerGameUnit);
        return rawDistance / worldUnitsPerGameUnit;

    }
}