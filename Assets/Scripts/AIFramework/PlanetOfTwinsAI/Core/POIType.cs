/// <summary>
/// Types of Points of Interest in Planet of Twins.
/// Used by POIManager for queries and by enemy AI for goal selection.
/// </summary>
public enum POIType
{
    Barrier,        // Central dividing structure — dark energy source
    SpawnPoint,     // Enemy spawn locations — breakable, defended
    RitualSite,     // Witness ritual locations — avoid twin path
    DarkEnergyZone, // High concentration dark energy areas
}