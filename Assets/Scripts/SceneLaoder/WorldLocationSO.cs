using UnityEngine;

/// <summary>
/// One asset per streamable world chunk.
/// Holds the scene to load and adjacency data.
///
/// SETUP:
///   Assets > Create > PlanetOfTwins > World Location
///   • Assign a scene from the Build Settings dropdown.
///   • Link adjacent locations by dragging other WorldLocationSO assets in.
///   • Mark isStartLocation on the first area the twins start in.
///
/// Arrival / spawn points are defined by:
///   • AreaSpawnPoints component in the scene  — for game-start positioning.
///   • LocationEntrance components in the scene — for directional arrivals
///     (set comesFrom = the WorldLocationSO of the area the twin came from).
///   No strings or IDs needed on this SO.
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/World Location", fileName = "Location_")]
public class WorldLocationSO : ScriptableObject
{
    [Header("Scene")]
    [Tooltip("The .unity scene that represents this chunk. Must be in Build Settings.")]
    public SceneReference scene;

    [Header("Adjacency")]
    [Tooltip("Locations directly reachable from here. " +
             "SceneFlowManager keeps these loaded whenever this location is occupied.")]
    public WorldLocationSO[] adjacentLocations;

    [Header("Startup")]
    [Tooltip("Mark the area where the twins start. " +
             "SceneFlowManager loads this first (alongside its adjacents).")]
    public bool isStartLocation;

    [Header("Audio (optional — null = silence, not an error)")]
    [Tooltip("Music for this area, crossfaded in by MusicManager when it becomes the active location.")]
    public MusicTrackData musicTrack;
    [Tooltip("Ambience bed + random one-shots for this area.")]
    public AmbienceData ambience;

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>True if this location's scene is valid and registered in Build Settings.</summary>
    public bool IsValid => scene.IsValid;

    public bool IsAdjacentTo(WorldLocationSO other)
    {
        if (adjacentLocations == null || other == null) return false;
        foreach (var adj in adjacentLocations)
            if (adj == other) return true;
        return false;
    }
}
