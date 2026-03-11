using System;

public interface ISharedHealthPool
{
    float CombinedHealth { get; }   // sum of both DisplayHealth values
    float MaxCombinedHealth { get; }   // 200 HP at base

    event Action<float> OnCombinedHealthChanged;
    event Action OnSharedPoolEmpty;
}