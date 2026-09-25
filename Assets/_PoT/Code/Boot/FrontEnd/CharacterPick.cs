/// <summary>
/// Couch M2 — a player's choice on the pre-game character-select screen.
///
/// Twin identity is POSITIONAL (project convention — see AccordSpiritSystem / AccordStateSystem):
///   <c>Lyra</c> = <c>PlayerRoster.TwinA</c> (left / Luminari),
///   <c>Kai</c>  = <c>PlayerRoster.TwinB</c> (right / Vethara).
///
/// <c>Random</c> defers the choice; <see cref="CharacterSelectController"/> resolves it to whichever
/// twin the other player did NOT take (both-Random → coin-flip), so the two players always end up on
/// distinct twins.
/// </summary>
public enum CharacterPick
{
    Lyra   = 0,
    Kai    = 1,
    Random = 2,
}
