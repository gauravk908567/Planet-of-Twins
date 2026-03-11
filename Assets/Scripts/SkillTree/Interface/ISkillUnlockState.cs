using System;

public interface ISkillUnlockState
{
    bool IsDualCastUnlocked { get; }
    bool IsCoalesceUnlocked { get; }
    bool IsSoulConvergenceUnlocked { get; }

    event Action OnDualCastUnlocked;
    event Action OnCoalesceUnlocked;
    event Action OnSoulConvergenceUnlocked;
}