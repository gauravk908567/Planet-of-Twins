using UnityEngine;

/// <summary>
/// Spawn timing and side configurations for a zone.
/// Split from AreaZoneConfig to keep it lean.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Spawn/Spawn Setup Config
/// </summary>
[CreateAssetMenu(fileName = "SpawnSetupConfig",
                 menuName = "PlanetOfTwins/Spawn/Spawn Setup Config")]
public class SpawnSetupConfig : ScriptableObject
{
    [Tooltip("Seconds between each spawn attempt per side.")]
    public float spawnInterval = 3f;

    public SideSpawnConfig leftSide;
    public SideSpawnConfig rightSide;

    public SideSpawnConfig GetSideConfig(SpawnSide side)
        => side == SpawnSide.Left ? leftSide : rightSide;
}