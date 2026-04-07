using UnityEngine;

[CreateAssetMenu(fileName = "LevelSpawnConfig",
                 menuName = "PlanetOfTwins/Level Spawn Config")]
public class LevelSpawnConfig : ScriptableObject
{
    [Tooltip("Must match the scene name exactly for auto-loading. " +
             "E.g. scene 'Level_01' → this field = 'Level_01'")]
    public string sceneName;

    [Header("Spawn Timing")]
    [Tooltip("Seconds between each spawn attempt per side")]
    public float spawnInterval = 3f;

    [Header("Left Side")]
    public SideSpawnConfig leftSide;

    [Header("Right Side")]
    public SideSpawnConfig rightSide;

    // ── Helpers ────────────────────────────────────────────────
    public SideSpawnConfig GetSideConfig(SpawnSide side)
        => side == SpawnSide.Left ? leftSide : rightSide;
}
