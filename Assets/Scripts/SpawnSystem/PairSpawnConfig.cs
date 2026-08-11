using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines what partner types this enemy prefers to spawn with.
/// One SO per enemy type — assigned on SideTypeEntry, reused across zones.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Spawn/Pair Spawn Config
/// </summary>
[CreateAssetMenu(fileName = "PairSpawnConfig",
                 menuName = "PlanetOfTwins/Spawn/Pair Spawn Config")]
public class PairSpawnConfig : ScriptableObject
{
    [Tooltip("Probability (0-1) that this enemy spawns with ANY partner.\n" +
             "0 = always solo. 1 = always paired.")]
    [Range(0f, 1f)]
    public float pairProbability = 0.8f;

    [Tooltip("Partner options — each rolls its own spawnProbability then weight.")]
    public PairPartnerEntry[] partners;

    /// <summary>
    /// Returns a chosen partner entry or null if no pair should spawn.
    /// </summary>
    public PairPartnerEntry PickPartner()
    {
        if (partners == null || partners.Length == 0) return null;
        if (Random.value > pairProbability) return null;

        var eligible = new List<PairPartnerEntry>();
        int totalWeight = 0;

        foreach (var p in partners)
        {
            if (p?.partnerPrefab == null) continue;
            if (Random.value > p.spawnProbability) continue;
            eligible.Add(p);
            totalWeight += p.weight;
        }

        if (eligible.Count == 0 || totalWeight == 0) return null;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var p in eligible)
        {
            cumulative += p.weight;
            if (roll < cumulative) return p;
        }

        return eligible[0];
    }
}