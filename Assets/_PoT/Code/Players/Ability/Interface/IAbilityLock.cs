public interface IAbilityLock
{
    void LockAbilities();
    void UnlockAbilities();
    bool AbilitiesLocked { get; }
}

