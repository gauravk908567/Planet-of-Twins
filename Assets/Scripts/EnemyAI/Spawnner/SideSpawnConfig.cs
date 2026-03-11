using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SideSpawnConfig
{
    [Tooltip("Hard cap — maximum enemies alive on this side at once")]
    public int maxTotalActive = 5;

    [Tooltip("Respawn triggers when active count drops below this % of maxTotalActive. " +
             "0.5 = respawn when below 50% (e.g. below 2-3 when max is 5)")]
    [Range(0.1f, 1f)]
    public float respawnThresholdPct = 0.5f;

    [Tooltip("Enemy types that can spawn on this side with their individual caps and weights")]
    public List<SideTypeEntry> entries = new List<SideTypeEntry>();

    // ── Computed threshold ─────────────────────────────────────
    public int RespawnThreshold => Mathf.CeilToInt(maxTotalActive * respawnThresholdPct);

    // ── Weighted random — respects per-type cap ────────────────
    // Returns null if no valid entry found (all at cap or list empty)
    public SideTypeEntry GetRandomEntry(Dictionary<SideTypeEntry, int> activeCounts)
    {
        if (entries == null || entries.Count == 0) return null;
        //foreach (var e in entries)
        //    Debug.Log($"[GetRandomEntry] entry prefab={e?.prefab?.name ?? "NULL"}, " + $"weight={e?.weight}, maxActiveOfType={e?.maxActiveOfType}");

        // Build eligible list — only entries below their per-type cap
        var eligible = new List<(SideTypeEntry entry, int weight)>();
        int totalWeight = 0;

        foreach (var entry in entries)
        {
            if (entry.prefab == null) continue;

            int currentActive = activeCounts.TryGetValue(entry, out int c) ? c : 0;
            bool belowTypeCap = entry.maxActiveOfType <= 0
                || currentActive < entry.maxActiveOfType;

            if (belowTypeCap)
            {
                eligible.Add((entry, entry.weight));
                totalWeight += entry.weight;
            }
        }

        if (eligible.Count == 0 || totalWeight == 0) return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var (entry, weight) in eligible)
        {
            cumulative += weight;
            if (roll < cumulative) return entry;
        }

        return eligible[0].entry;
    }
}
