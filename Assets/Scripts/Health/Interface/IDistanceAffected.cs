public interface IDistanceAffected
{
    /// <summary>
    /// Called by TwinBondManager every frame with the latest
    /// distance modifier (0–1).
    /// </summary>
    void SetDistanceModifier(float modifier);
}