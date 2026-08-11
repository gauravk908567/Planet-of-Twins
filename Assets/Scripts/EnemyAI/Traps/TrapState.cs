public enum TrapState
{
    Dormant,    // waiting — player hasn't stepped on it yet
    Arming,     // 1s countdown before it can grab
    Active,     // 2s grab window — OverlapSphere each frame
    Dragging,   // player grabbed — TTK countdown running
    Released,   // player rescued successfully
    PossessedActive,
    Killed      // TTK reached 0, player died
}