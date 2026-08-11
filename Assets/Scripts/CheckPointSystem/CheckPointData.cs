using UnityEngine;

/// <summary>
/// Snapshot of game state at a checkpoint.
/// Intentionally minimal for prototype ï¿½ extend fields as the game grows.
/// </summary>
[System.Serializable]
public class CheckpointData
{
    // Positions
    public Vector3 leftTwinPosition;
    public Vector3 rightTwinPosition;

    // Economy
    public int skillPoints;

    // Skill-tree progress at save time. Dictionary keyed by SO reference (in-memory only).
    // Captures all 9 trees regardless of list order â€” replaces the brittle int[] parallel array.
    public SkillTreeRuntimeState.Snapshot skillTreeSnapshot;

    // HP is always restored to full on respawn (by design).

    // Sword pickup state ï¿½ did each twin have the sword when checkpoint was saved?
    public bool leftHasSword;
    public bool rightHasSword;

    // The area where this checkpoint lives — SoftResetController streams it in before teleporting.
    public WorldLocationSO checkpointLocation;

    // Which world-chunk scenes were loaded when this checkpoint was saved.
    public WorldLocationSO[] activeLocations;
}
