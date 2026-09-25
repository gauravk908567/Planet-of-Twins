using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines which commander groups can spawn in a zone and at what weight.
/// Referenced by AreaZoneConfig.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Spawn/Group Spawn Config
/// </summary>
[CreateAssetMenu(fileName = "GroupSpawnConfig",
                 menuName = "PlanetOfTwins/Spawn/Group Spawn Config")]
public class GroupSpawnConfig : ScriptableObject
{
    [Tooltip("Overall probability (0-1) a commander group spawns on zone activation.")]
    [Range(0f, 1f)] public float groupSpawnProbability = 0.3f;

    [Tooltip("Max commander groups active simultaneously in this zone.")]
    public int maxActiveGroups = 1;

    [Tooltip("Seconds between group respawn attempts after previous group is wiped.")]
    public float groupRespawnDelay = 30f;

    public GroupSpawnEntry[] groups;

    /// <summary>Returns a chosen group definition or null.</summary>
    public CommanderGroupDefinition PickGroup()
    {
        if (groups == null || groups.Length == 0) return null;
        if (Random.value > groupSpawnProbability) return null;

        var eligible = new List<GroupSpawnEntry>();
        int totalWeight = 0;
        foreach (var g in groups)
        {
            if (g?.groupDefinition == null) continue;
            eligible.Add(g);
            totalWeight += g.weight;
        }

        if (eligible.Count == 0 || totalWeight == 0) return null;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var g in eligible)
        {
            cumulative += g.weight;
            if (roll < cumulative) return g.groupDefinition;
        }
        return eligible[0].groupDefinition;
    }
}

[System.Serializable]
public class GroupSpawnEntry
{
    public CommanderGroupDefinition groupDefinition;
    [Range(1, 10)] public int weight = 1;
}