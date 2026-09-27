using System.Collections.Generic;

/// <summary>
/// The streaming decision with no scenes or coroutines, so it can be tested (P8.1): given where every actor is
/// (both twins and the rescue soul), which locations should be loaded, and may a loaded one unload.
/// <see cref="SceneFlowManager"/> owns the actors' locations and runs the actual loads and unloads.
///
/// Rules (the Graves transition model, see SceneFlowManager):
///   • desired = every occupied location + its valid adjacents;
///   • a loaded location may unload only when it is neither desired nor occupied. A stale actor location can only
///     keep an extra area loaded (safe); it can never unload occupied ground (unsafe).
/// </summary>
public static class OccupancyModel
{
    public static HashSet<WorldLocationSO> BuildDesiredSet(IEnumerable<WorldLocationSO> actorLocations)
    {
        var desired = new HashSet<WorldLocationSO>();
        foreach (var loc in actorLocations)
        {
            if (loc == null) continue;
            desired.Add(loc);
            if (loc.adjacentLocations == null) continue;
            foreach (var adj in loc.adjacentLocations)
                if (adj != null && adj.IsValid) desired.Add(adj);
        }
        return desired;
    }

    public static bool IsOccupied(WorldLocationSO location, IEnumerable<WorldLocationSO> actorLocations)
    {
        foreach (var loc in actorLocations)
            if (loc == location) return true;
        return false;
    }

    public static bool MayUnload(WorldLocationSO location, HashSet<WorldLocationSO> desired,
                                 IEnumerable<WorldLocationSO> actorLocations)
        => !desired.Contains(location) && !IsOccupied(location, actorLocations);
}
