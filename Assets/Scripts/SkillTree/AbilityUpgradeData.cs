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

    // ── Empower base values ───────────────────────────────────
    [Header("Empower base values")]
    public float baseEmpowerDuration = 6f;
    public float baseEmpowerCooldown = 12f;
    public float baseEmpowerKnockbackInterval = 1.5f;
    public float baseEmpowerKnockbackForce = 8f;
    public float baseEmpowerKnockbackRadius = 5f;

    // ── Accord State base values ──────────────────────────────
    [Header("Accord State base values")]
    public float baseAccordDuration = 12f;
    public float baseAccordBarCap = 35f;
    public float baseAccordShockwaveForce = 15f;
    public float baseAccordMeleeSlowDuration = 1.25f;
    public float baseAccordMeleeAggroLossDuration = 0.75f;

    // ── Gate pulse (Weaver's Gate + Ashen Tide) base values ──────
    [Header("Gate Pulse — Weaver's Gate slow pulse base values")]
    [Tooltip("Seconds between auto-pulses while soul is active.")]
    public float basePulseInterval = 3.5f;
    [Tooltip("Radius of each pulse around the soul.")]
    public float basePulseRadius = 4f;
    [Tooltip("How long enemies are feared per pulse hit.")]
    public float basePulseFearDuration = 1.5f;
    [Tooltip("Speed multiplier applied to feared enemies. 0.6 = -40% speed.")]
    public float basePulseSlowMultiplier = 0.6f;
    [Tooltip("How long the slow lasts per pulse hit.")]
    public float basePulseSlowDuration = 1.5f;

    [Header("Ashen Tide — Accord mode burn values")]
    [Tooltip("Burn damage per second applied to enemies hit by pulse in Accord mode.")]
    public float baseBurnDps = 0.35f;
    [Tooltip("Burn duration added per pulse hit. Stacks with multiple pulse hits.")]
    public float baseBurnDuration = 1.8f;

    [Header("Upgrade nodes — filled manually by designer")]
    public List<AbilityUpgradeNode> nodes = new List<AbilityUpgradeNode>();

    // Delegates to SkillTreeManager runtime state (R7 — SO is config only, no runtime mutation).
    // All existing readers (CoalesceSystem, SkillNodeButton, etc.) continue to work unchanged.
    public int currentNodeIndex => SkillTreeManager.Instance?.GetLevel(this) ?? 0;
    public bool IsMaxed => currentNodeIndex >= nodes.Count;

    // ── Checkpoint support ────────────────────────────────────
    public int? CurrentUnlockedLevel => currentNodeIndex;

    // ── Core computed values ──────────────────────────────────
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

    // ── Empower computed values ───────────────────────────────
    public float CurrentEmpowerDuration
    {
        get
        {
            float total = baseEmpowerDuration;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].empowerDurationBonus;
            return Mathf.Max(0.5f, total);
        }
    }

    public float CurrentEmpowerCooldown
    {
        get
        {
            float total = baseEmpowerCooldown;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total -= nodes[i].empowerCooldownReduction;
            return Mathf.Max(1f, total);
        }
    }

    public float CurrentEmpowerKnockbackInterval
    {
        get
        {
            float total = baseEmpowerKnockbackInterval;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total -= nodes[i].empowerKnockbackIntervalReduction;
            return Mathf.Max(0.2f, total);
        }
    }

    public float CurrentEmpowerKnockbackForce
    {
        get
        {
            float total = baseEmpowerKnockbackForce;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].empowerKnockbackForceBonus;
            return Mathf.Max(0f, total);
        }
    }

    public float CurrentEmpowerKnockbackRadius
    {
        get
        {
            float total = baseEmpowerKnockbackRadius;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].empowerKnockbackRadiusBonus;
            return Mathf.Max(0.5f, total);
        }
    }

    // ── Accord State computed values ──────────────────────────
    public float CurrentAccordDuration
    {
        get
        {
            float total = baseAccordDuration;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].accordDurationBonus;
            return Mathf.Max(1f, total);
        }
    }

    public float CurrentAccordBarCap
    {
        get
        {
            float total = baseAccordBarCap;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total -= nodes[i].accordBarCapReduction;
            return Mathf.Max(5f, total); // floor at 5 — never trivially easy
        }
    }

    public float CurrentAccordShockwaveForce
    {
        get
        {
            float total = baseAccordShockwaveForce;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].accordShockwaveForceBonus;
            return Mathf.Max(0f, total);
        }
    }

    public float CurrentAccordMeleeSlowDuration
    {
        get
        {
            float total = baseAccordMeleeSlowDuration;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].accordMeleeSlowBonus;
            return Mathf.Max(0.1f, total);
        }
    }

    public float CurrentAccordMeleeAggroLossDuration
    {
        get
        {
            float total = baseAccordMeleeAggroLossDuration;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].accordMeleeAggroLossBonus;
            return Mathf.Max(0.1f, total);
        }
    }

    // ── Gate Pulse computed values ────────────────────────────
    public float CurrentPulseInterval
    {
        get
        {
            float total = basePulseInterval;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total -= nodes[i].pulseIntervalReduction;
            return Mathf.Max(0.5f, total);
        }
    }

    public float CurrentPulseRadius
    {
        get
        {
            float total = basePulseRadius;
            for (int i = 0; i < currentNodeIndex && i < nodes.Count; i++)
                total += nodes[i].pulseRadiusBonus;
            return Mathf.Max(1f, total);
        }
    }

    // ── Economy ───────────────────────────────────────────────
    public bool HasNextNode => currentNodeIndex < nodes.Count;
    public int TotalNodes => nodes.Count;
    public int NextNodeCost => HasNextNode ? nodes[currentNodeIndex].pointCost : 0;

    // UnlockNextNode() and ResetToBase() removed — SkillTreeManager mutates
    // _runtimeState directly. Call SkillTreeManager.TryPurchaseNode(data) to purchase.
}