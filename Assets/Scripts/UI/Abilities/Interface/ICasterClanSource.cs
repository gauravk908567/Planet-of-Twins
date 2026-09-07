/// <summary>
/// Optional companion to <see cref="IAbilityHUDSource"/> for JOINT abilities that are cast by ONE twin (e.g. Empower):
/// while being charged / active, the card's glow, implosion and release-pulse take the CASTER's single clan colour
/// instead of the dual gold/violet split (the border keeps the split as the joint identity). BorderFillDriver checks
/// for this via `is ICasterClanSource` — abilities that don't have a caster simply don't implement it (no interface
/// pollution, no per-ability wiring).
/// </summary>
public interface ICasterClanSource
{
    /// <summary>Which clan is casting: -1 = none (use the dual split), 0 = LEFT/clanA (Lyra gold),
    /// 1 = RIGHT/clanB (Kai violet). Valid while charging or active; -1 otherwise.</summary>
    int CasterSide { get; }
}
