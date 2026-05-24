using UnityEngine;

/// <summary>
/// Defines a commander and its formation group.
/// One SO per commander type. Referenced by GroupSpawnConfig.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Spawn/Commander Group Definition
/// </summary>
[CreateAssetMenu(fileName = "CommanderGroupDef",
                 menuName = "PlanetOfTwins/Spawn/Commander Group Definition")]
public class CommanderGroupDefinition : ScriptableObject
{
    [Header("Commander")]
    public GameObject commanderPrefab;
    public EnemyData commanderData;

    [Header("Formation")]
    [Tooltip("Each slot defines one soldier. Offset = local space from commander.")]
    public FormationSlot[] slots;

    [Header("Spawn")]
    public SpawnSide spawnSide = SpawnSide.Left;

    [Header("Behaviour")]
    [Tooltip("Seconds soldiers rage after commander dies before going feral.")]
    public float commanderDeathRageDuration = 4.5f;

    [Tooltip("Radius within which soldiers respond to commander.")]
    public float commandRadius = 15f;
}