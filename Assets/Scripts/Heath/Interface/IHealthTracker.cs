using System;

public interface IHealthTracker
{
    float CombatHealth { get; }   // raw HP, ignores distance
    float DisplayHealth { get; }   // ratio-preserving: combatHP × modifier
    float MaxHealth { get; }
    float CurrentModifier { get; }
    bool IsDead { get; }

    event Action<float> OnDisplayHealthChanged;  // fires when displayed value changes
    event Action OnDeath;
}