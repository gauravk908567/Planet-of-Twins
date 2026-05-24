using System;
using UnityEngine;
using UnityEngine.Video;

[Serializable]
public class AbilityUpgradeNode
{
    [Tooltip("Name shown on the button in the skill tree UI")]
    public string label;

    [Tooltip("Skill-point cost to unlock this node")]
    public int pointCost = 3;

    // ── Preview video ─────────────────────────────────────────
    [Header("Preview")]
    [Tooltip("Short looping clip showing what this node does. " +
             "Plays silently in the card thumbnail. " +
             "Leave empty — card shows a placeholder colour instead.")]
    public VideoClip previewClip;

    // ── Core ability stats ────────────────────────────────────
    [Tooltip("Duration added in seconds (0 = no change)")]
    public float durationBonus;

    [Tooltip("Cooldown REDUCED by this many seconds (0 = no change)")]
    public float cooldownReduction;

    [Tooltip("Additional targets unlocked (0 = no change)")]
    public int targetCountBonus;

    [Tooltip("Range added in units (0 = no change)")]
    public float rangeBonus;

    [Tooltip("Cooldown reduction in seconds — subtracted from base cooldown (0 = no change)")]
    public float cooldownBonus;

    // ── Health regen ──────────────────────────────────────────
    [Tooltip("Multiplied onto current regen rate. 1 = no change.")]
    public float regenRateMultiplier = 1f;

    // ── Coalesce-specific ─────────────────────────────────────
    [Tooltip("Coalesce aura radius added in units (0 = no change)")]
    public float coalesceRadiusBonus;

    [Tooltip("Coalesce aura linger duration added in seconds (0 = no change)")]
    public float coalesceDurationBonus;

    [Tooltip("Coalesce damage per second added (0 = no change)")]
    public float coalesceDpsBonus;

    // ── Soul Convergence-specific ─────────────────────────────
    [Tooltip("Kill threshold REDUCED by this many kills (0 = no change)")]
    public int soulThresholdReduction;

    [Tooltip("Power-state duration added in seconds (0 = no change)")]
    public float soulDurationBonus;

    // ── Empower-specific ──────────────────────────────────────
    [Header("Empower")]
    [Tooltip("Active duration added in seconds (0 = no change)")]
    public float empowerDurationBonus;

    [Tooltip("Cooldown REDUCED by this many seconds (0 = no change)")]
    public float empowerCooldownReduction;

    [Tooltip("Knockback interval REDUCED by this many seconds (0 = no change)")]
    public float empowerKnockbackIntervalReduction;

    [Tooltip("Knockback force added (0 = no change)")]
    public float empowerKnockbackForceBonus;

    [Tooltip("Knockback radius added in units (0 = no change)")]
    public float empowerKnockbackRadiusBonus;

    // ── Accord State-specific ─────────────────────────────────
    [Header("Accord State")]
    [Tooltip("Active duration added in seconds (0 = no change)")]
    public float accordDurationBonus;

    [Tooltip("Bar cap REDUCED by this many points — fills faster (0 = no change)")]
    public float accordBarCapReduction;

    [Tooltip("Shockwave force added on activation (0 = no change)")]
    public float accordShockwaveForceBonus;

    [Tooltip("Accord E slow duration added in seconds (0 = no change)")]
    public float accordMeleeSlowBonus;

    [Tooltip("Accord E aggro loss duration added in seconds (0 = no change)")]
    public float accordMeleeAggroLossBonus;

    // ── Gate Pulse (Weaver's Gate + Ashen Tide) ──────────────
    [Header("Gate Pulse")]
    [Tooltip("Pulse interval REDUCED by this many seconds — pulses fire faster (0 = no change)")]
    public float pulseIntervalReduction;

    [Tooltip("Pulse radius added in units (0 = no change)")]
    public float pulseRadiusBonus;

    // ── UI ────────────────────────────────────────────────────
    [Tooltip("Tooltip shown beneath the button in the skill tree panel")]
    [TextArea] public string description;
}