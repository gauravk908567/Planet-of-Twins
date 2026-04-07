using UnityEngine;

/// <summary>
/// Snapshot of game state at a checkpoint.
/// Intentionally minimal for prototype — extend fields as the game grows.
/// </summary>
[System.Serializable]
public class CheckpointData
{
    // Positions
    public Vector3 leftTwinPosition;
    public Vector3 rightTwinPosition;

    // Economy
    public int skillPoints;

    // Upgrade node states — stored as parallel arrays matching
    // the order in SkillTreeManager.AllData(). Brittle if SO order changes,
    // but sufficient for prototype. Replace with a Dictionary<string,int>
    // keyed by SO asset GUID for a production system.
    public int[] nodeUnlockLevels;

    // HP is always restored to full on respawn (by design).

    // Sword pickup state — did each twin have the sword when checkpoint was saved?
    public bool leftHasSword;
    public bool rightHasSword;
}