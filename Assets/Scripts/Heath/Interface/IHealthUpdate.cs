using System;

public interface IHealthUpdate
{
    float MaxHealth { get; }
    float FinalHealth { get; }
    event Action <float> OnHealthUpdated;
}