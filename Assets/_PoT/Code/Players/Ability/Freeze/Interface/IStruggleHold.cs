/// <summary>
/// Something holding a twin that the twin escapes by mashing its MELEE button (BUG-144). Today: the Tether-Breaker's
/// chain. The holder puts itself on the caught twin (<see cref="Player.SetStruggleHold"/>) and clears itself on EVERY
/// release path; <see cref="TwinAttackDispatcher"/> then turns that twin's melee presses into <see cref="OnStruggle"/>
/// instead of swings ("held, no melee", the same rule as a rescue grab).
/// Rescue traps keep their own path (<see cref="IRescueTarget.OnStruggle"/>, driven by RescueEventController).
/// </summary>
public interface IStruggleHold
{
    /// <summary>One mash press from the held twin's own player.</summary>
    void OnStruggle();
}
