/// <summary>
/// Implemented by any ability system that has a meaningful "currently active"
/// state that should block other systems from activating.
///
/// AccordStateSystem reads this on both SoulConvergenceSystem and EmpowerSystem
/// before allowing Accord activation — if either is active, Accord is blocked
/// until the ability ends or is cancelled.
///
/// Deliberately minimal — one property, no coupling.
/// </summary>
public interface IAbilityActiveState
{
    /// <summary>
    /// True when the ability is in its active power state.
    /// For SoulConvergenceSystem: true during the 7s buff window.
    /// For EmpowerSystem: true during the anchored duration.
    /// False during charging, cooldown, or idle.
    /// </summary>
    bool IsAbilityActive { get; }
}