
/// <summary>
/// Defines one contiguous piecewise-linear distance band.
/// healthAtStart and healthAtEnd are in percent (0–100).
/// </summary>
[System.Serializable]
public struct DistanceZone
{
    public float DistanceStart;
    public float DistanceEnd;
    public float HealthPercentAtStart;   // percent at DistanceStart
    public float HealthPercentAtEnd;     // percent at DistanceEnd
}

