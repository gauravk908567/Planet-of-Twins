using UnityEngine;

/// <summary>
/// A single soldier slot in a commander formation.
/// Offset is in local space relative to commander spawn point.
/// X = left/right, Z = forward/back relative to commander facing.
/// </summary>
[System.Serializable]
public class FormationSlot
{
    public GameObject prefab;
    public EnemyData data;

    [Tooltip("Local space offset from commander position.")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Designer label only — not used by code.")]
    public string roleLabel = "Soldier";
}