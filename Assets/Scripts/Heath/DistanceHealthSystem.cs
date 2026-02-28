using UnityEngine;

public class DistanceHealthSystem : MonoBehaviour
{
    public float CalculateDistanceModifier(float distance, int upgradeLevel)
    {
        if (distance <= 6f) return 1f;
        if (distance > 18f) return 0f;

        float basePercent = 0f;

        if (distance > 6f && distance <= 12f)
        {
            float t = Mathf.InverseLerp(6f, 12f, distance);
            basePercent = Mathf.Lerp(100f, 50f, t);
        }
        else if (distance > 12f && distance <= 16f)
        {
            float t = Mathf.InverseLerp(12f, 16f, distance);
            basePercent = Mathf.Lerp(50f, 25f, t);
        }
        else if (distance > 16f && distance <= 18f)
        {
            float t = Mathf.InverseLerp(16f, 18f, distance);
            basePercent = Mathf.Lerp(25f, 1f, t);
        }

        float bonus = (9f * upgradeLevel) - (upgradeLevel * upgradeLevel);

        float finalPercent = Mathf.Clamp(basePercent + bonus, 0f, 100f);

        return finalPercent / 100f; // return as 0–1 multiplier
    }
}