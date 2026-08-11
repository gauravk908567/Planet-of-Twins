using System;

public interface IPointBank
{
    int CurrentPoints { get; }
    void AddPoints(int amount);
    bool TrySpendPoints(int amount);          // returns false if insufficient

    event Action<int> OnPointsChanged;        // arg = new total
}