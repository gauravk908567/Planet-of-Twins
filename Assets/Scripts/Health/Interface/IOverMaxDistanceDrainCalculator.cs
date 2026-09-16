public interface IOverMaxDistanceDrainCalculator
{
    /// <summary>
    /// Call every frame when distance > 18u.
    /// Returns the cumulative fraction of health that has drained (0–1).
    /// </summary>
    float UpdateDrain(float deltaTime);

    /// <summary>
    /// Call every frame when distance ≤ 18u.
    /// Reduces drain fraction back toward 0 at recovery rate.
    /// Returns the remaining drain fraction.
    /// </summary>
    float UpdateRecovery(float deltaTime);

    /// <returns>Current accumulated drain fraction (0–1).</returns>
    float CurrentDrainFraction { get; }

    void Reset();
}