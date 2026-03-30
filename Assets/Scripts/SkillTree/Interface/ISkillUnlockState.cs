using System;

public interface ISkillUnlockState
{
    bool IsAccordSpiritsUnlocked { get; }
    bool IsCoalesceUnlocked { get; }
    bool IsSoulConvergenceUnlocked { get; }
    bool IsEmpowerUnlocked { get; }
    bool IsAccordStateUnlocked { get; }

    event Action OnAccordSpiritsUnlocked;
    event Action OnCoalesceUnlocked;
    event Action OnSoulConvergenceUnlocked;
    event Action OnEmpowerUnlocked;
    event Action OnAccordStateUnlocked;
}