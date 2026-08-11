public interface IDistanceModifierCalculator
{
    /// <summary>
    /// Returns a 0–1 health multiplier for the given distance and upgrade node.
    /// 0 = no health capacity.  1 = full health capacity.
    /// Does NOT handle >18u drain — that is IOver18DrainCalculator's responsibility.
    /// </summary>
    float CalculateModifier(float distance, int upgradeNode);
}