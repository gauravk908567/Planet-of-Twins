using UnityEngine;

public class TwinDistanceMonitor : MonoBehaviour, IDistanceProvider
{
    [SerializeField] private Transform leftTwin;
    [SerializeField] private Transform RightTwin;

    public float GetDistance()
    {
        if (leftTwin == null || RightTwin == null) 
            return 0f;

        return Vector3.Distance(leftTwin.position, RightTwin.position);
    }
}
