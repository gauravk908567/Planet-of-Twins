using UnityEngine;
public class Over18DrainCalculator : IOverMaxDistanceDrainCalculator
{
    // GDD values — injected so they can be overridden without modifying this class
    private readonly float _drainRatePerSecond;     // default 25 HP/s → 0.25 per sec on 0-1 scale
    private readonly float _recoveryRatePerSecond;  // default 15 HP/s → 0.15 per sec

    private float _accumulatedDrainFraction;

    public float CurrentDrainFraction => _accumulatedDrainFraction;

    public Over18DrainCalculator(float drainRatePerSecond = 0.25f,
                                 float recoveryRatePerSecond = 0.15f)
    {
        _drainRatePerSecond = drainRatePerSecond;
        _recoveryRatePerSecond = recoveryRatePerSecond;
    }

    public float UpdateDrain(float deltaTime)
    {
        _accumulatedDrainFraction =
            Mathf.Clamp01(_accumulatedDrainFraction + _drainRatePerSecond * deltaTime);
        return _accumulatedDrainFraction;
    }

    public float UpdateRecovery(float deltaTime)
    {
        _accumulatedDrainFraction =
            Mathf.Clamp01(_accumulatedDrainFraction - _recoveryRatePerSecond * deltaTime);
        return _accumulatedDrainFraction;
    }

    public void Reset() => _accumulatedDrainFraction = 0f;
}
