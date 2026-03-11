using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// AbilityUpgradeNode
// One step on a linear upgrade chain. Serialised inside AbilityUpgradeData SO.
// Fill these in the Inspector on each SO — no code changes needed for tuning.
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class AbilityUpgradeNode
{
    [Tooltip("Name shown on the button in the skill tree UI")]
    public string label;

    [Tooltip("Skill-point cost to unlock this node")]
    public int pointCost = 3;

    // ── Core ability stats ────────────────────────────────────────────────────
    [Tooltip("Duration added in seconds (0 = no change)")]
    public float durationBonus;

    [Tooltip("Cooldown REDUCED by this many seconds — positive = shorter CD (0 = no change)")]
    public float cooldownReduction;

    [Tooltip("Additional targets unlocked (0 = no change)")]
    public int targetCountBonus;

    [Tooltip("Range added in units (0 = no change)")]
    public float rangeBonus;

    [Tooltip("Cooldown reduction in seconds — subtracted from base cooldown (0 = no change)")]
    public float cooldownBonus;

    // ── Health regen ──────────────────────────────────────────────────────────
    [Tooltip("Multiplied onto current regen rate. 1 = no change. Use 1.333 for ×4/3.")]
    public float regenRateMultiplier = 1f;

    // ── Coalesce-specific ─────────────────────────────────────────────────────
    [Tooltip("Coalesce aura radius added in units (0 = no change)")]
    public float coalesceRadiusBonus;

    [Tooltip("Coalesce aura linger duration added in seconds (0 = no change)")]
    public float coalesceDurationBonus;

    [Tooltip("Coalesce damage per second added (0 = no change)")]
    public float coalesceDpsBonus;

    // ── Soul Convergence-specific ─────────────────────────────────────────────
    [Tooltip("Kill threshold REDUCED by this many kills (0 = no change)")]
    public int soulThresholdReduction;

    [Tooltip("Power-state duration added in seconds (0 = no change)")]
    public float soulDurationBonus;

    // ── UI ────────────────────────────────────────────────────────────────────
    [Tooltip("Tooltip shown beneath the button in the skill tree panel")]
    [TextArea] public string description;
}