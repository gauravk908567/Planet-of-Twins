using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AbilityUpgradeData",
                 menuName = "PlanetOfTwins/Ability Upgrade Data")]
public class AbilityUpgradeData : ScriptableObject
{
    [Header("Base values (before any upgrades)")]
    public int baseTargetCount = 1;
    public float baseDuration = 3f;
    public float baseRange = 5f;
    public float baseCooldown = 8f;

    [Header("Upgrade nodes — filled manually by designer")]
    public List<AbilityUpgradeNode> nodes = new List<AbilityUpgradeNode>();

    public int currentNodeIndex = 0;
    public bool IsMaxed => currentNodeIndex >= nodes.Count;

    // ── Checkpoint support ────────────────────────────────────
    /// <summary>
    /// How many nodes are currently unlocked. Read by CheckpointManager
    /// to snapshot state, then used to re-unlock the correct number on respawn.
    /// Same as currentNodeIndex — exposed as a nullable int so CheckpointManager
    /// can handle a null AbilityUpgradeData slot safely.
    /// </summary>
    public int? CurrentUnlockedLevel => currentNodeIndex;

    // ── Computed values ───────────────────────────────────────
    public int CurrentMaxTargets
    {
        get
        {
            int total = baseTargetCount;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].targetCountBonus;
            return Mathf.Max(1, total);
        }
    }

    public float CurrentDuration
    {
        get
        {
            float total = baseDuration;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].durationBonus;
            return Mathf.Max(0.1f, total);
        }
    }

    public float CurrentRange
    {
        get
        {
            float total = baseRange;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].rangeBonus;
            return Mathf.Max(0.5f, total);
        }
    }

    public float CurrentCooldown
    {
        get
        {
            float total = baseCooldown;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total -= nodes[i].cooldownBonus;
            return Mathf.Max(0.5f, total);
        }
    }

    // ── Economy ───────────────────────────────────────────────
    public bool HasNextNode => currentNodeIndex < nodes.Count;
    public int TotalNodes => nodes.Count;
    public int NextNodeCost => HasNextNode ? nodes[currentNodeIndex].pointCost : 0;

    public void UnlockNextNode()
    {
        if (HasNextNode) currentNodeIndex++;
    }

    public void ResetToBase()
    {
        currentNodeIndex = 0;
    }
}