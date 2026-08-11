using UnityEngine;

/// <summary>
/// Shared VELOCITY profile for travel visuals (Weaver's Gate soul flight + death helix ascent —
/// user spec, playtest round 3, modeled on Kiriko's teleport feel):
///   • first ~15% of the distance: speed ramps 0→max on a curve that starts gradual and gets
///     STEEPER (x², slow lift-off, snapping to full speed),
///   • middle: full speed,
///   • last ~10%: speed falls max→0 on a curve that also starts gradual and gets steeper
///     (√x — barely slowing at first, plunging right at the arrival point).
/// Distance-domain: callers pass progress 0–1 along the route and multiply their max speed by
/// <see cref="SpeedMultiplier"/>. Self-timed drivers (death helix) divide by
/// <see cref="AverageMultiplier"/> so their authored duration stays the true total time.
/// A small floor guarantees motion at the endpoints (x² is exactly 0 at launch).
/// </summary>
public static class TravelEase
{
    public const float RampInFraction = 0.15f;   // accelerate over the first 15% of the route
    public const float RampOutFraction = 0.10f;  // decelerate over the last 10%
    private const float FloorMultiplier = 0.12f; // never fully stall at the endpoints

    /// <summary>Speed multiplier (0–1] at a given distance progress 0–1 along the route.</summary>
    public static float SpeedMultiplier(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        float mul = 1f;
        if (p < RampInFraction)
        {
            float x = p / RampInFraction;
            mul = x * x;                    // gradual start, steepening gain
        }
        float remaining = 1f - p;
        if (remaining < RampOutFraction)
        {
            float y = remaining / RampOutFraction;
            mul = Mathf.Min(mul, Mathf.Sqrt(y));   // gradual fall-off, steepening into the stop
        }
        return Mathf.Max(FloorMultiplier, mul);
    }

    /// <summary>Mean of <see cref="SpeedMultiplier"/> over the route — normalizes self-timed drivers.</summary>
    public static readonly float AverageMultiplier = ComputeAverage();

    private static float ComputeAverage()
    {
        const int samples = 256;
        float sum = 0f;
        for (int i = 0; i < samples; i++)
            sum += SpeedMultiplier((i + 0.5f) / samples);
        return sum / samples;
    }
}
