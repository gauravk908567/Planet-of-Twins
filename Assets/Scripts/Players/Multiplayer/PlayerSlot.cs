/// <summary>
/// Which local player drives a twin. M1 hardcodes One→TwinA, Two→TwinB; character select (M2)
/// rewrites the mapping via <see cref="PlayerRoster.Assign"/>. Kept as an explicit enum (not a raw
/// int) so ownership reads self-document and the char-select/input-routing code can't transpose slots.
/// </summary>
public enum PlayerSlot
{
    One = 0,
    Two = 1,
}
