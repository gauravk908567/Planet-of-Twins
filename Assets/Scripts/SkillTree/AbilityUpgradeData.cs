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

    // Written by SkillTreeManager when a node is purchased. Do not write elsewhere.
    public int currentNodeIndex = 0;
    public bool IsMaxed => currentNodeIndex >= nodes.Count;

    // ── Computed values used by abilities ────────────────────────────────────

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

    // Cooldown decreases with upgrades — cooldownBonus is subtracted.
    public float CurrentCooldown
    {
        get
        {
            float total = baseCooldown;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total -= nodes[i].cooldownBonus;
            return Mathf.Max(0.5f, total); // floor at 0.5s — never zero
        }
    }

    // ── Economy ──────────────────────────────────────────────────────────────

    public bool HasNextNode => currentNodeIndex < nodes.Count;
    public int TotalNodes => nodes.Count;

    /// <summary>Cost of the next node to purchase. 0 if no nodes remain.</summary>
    public int NextNodeCost => HasNextNode ? nodes[currentNodeIndex].pointCost : 0;

    /// <summary>Called by SkillTreeManager to advance to the next node.</summary>
    public void UnlockNextNode()
    {
        if (HasNextNode) currentNodeIndex++;
    }

    /// <summary>
    /// Called by SkillTreeManager.Awake() — resets runtime state written during
    /// a previous play session. ScriptableObjects are assets and persist across
    /// editor play sessions without this reset.
    /// </summary>
    public void ResetToBase()
    {
        currentNodeIndex = 0;
    }
}