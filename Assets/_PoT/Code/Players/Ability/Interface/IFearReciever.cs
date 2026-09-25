/// <summary>
/// Implemented by enemies that can be feared — temporarily fleeing from a position.
/// SoulPulseSystem calls ApplyFear on enemies hit by the soul pulse.
/// </summary>
public interface IFearReceiver
{
    /// <summary>
    /// Apply fear state — enemy flees from fleeFrom position for duration seconds.
    /// If already feared, resets duration.
    /// </summary>
    void ApplyFear(UnityEngine.Vector3 fleeFrom, float duration);
}