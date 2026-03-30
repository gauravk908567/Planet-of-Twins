/// <summary>
/// Implemented by enemies that support timed speed reduction.
/// Each slow registers with a sourceKey so multiple slows stack multiplicatively
/// and expire independently.
/// </summary>
public interface ISlowReceiver
{
    /// <summary>
    /// Apply a speed multiplier (e.g. 0.6f = -40% speed) for duration seconds.
    /// sourceKey identifies this slow so it can be overwritten or removed cleanly.
    /// </summary>
    void ApplySlow(float speedMultiplier, float duration, string sourceKey);
}