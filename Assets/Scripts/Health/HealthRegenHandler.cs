using UnityEngine;

public class HealthRegenHandler
{
    private static readonly float[] RegenRates = { 16.6f, 22.1f, 27.7f, 33.3f };
    private static readonly float[] RegenDelays = { 6.0f, 4.51f, 3.61f, 3.0f };

    private int _upgradeNode;
    private float _timeSinceLastCombatDamage;
    private bool _regenPaused;

    // BUG FIX 1: _hasEverTakenDamage guards against regen firing on a
    // fresh/respawned player who hasn't been hit yet.
    // Without this, a newly spawned player would start regenerating after
    // RegenDelays[0] = 6s even with full HP (GetRegenThisFrame early-exits on
    // maxHealth, but if they take any damage and are still above 0 they'd regen
    // immediately if the delay had already elapsed from spawn time).
    private bool _hasEverTakenDamage;

    public void SetUpgradeNode(int node)
    {
        _upgradeNode = Mathf.Clamp(node, 0, RegenRates.Length - 1);
    }

    public void OnCombatDamageTaken()
    {
        _timeSinceLastCombatDamage = 0f;
        _hasEverTakenDamage = true;
    }

    public void PauseRegen() => _regenPaused = true;
    public void ResumeRegen() => _regenPaused = false;

    /// <summary>
    /// Called by CheckpointManager via PlayerHealthComponent.RestoreToFull().
    /// Clears timer and damage history so a respawned player behaves like a
    /// freshly spawned one  no regen until they take their first hit.
    /// </summary>
    public void Reset()
    {
        _timeSinceLastCombatDamage = 0f;
        _hasEverTakenDamage = false;
        _regenPaused = false;
    }

    /// <summary>
    /// True when regen is actively ticking this frame.
    /// Used by HealthRegenVFX to drive the particle system.
    /// </summary>
    public bool IsRegenerating(float currentCombatHealth, float maxHealth)
    {
        if (_regenPaused) return false;
        if (!_hasEverTakenDamage) return false;
        if (currentCombatHealth >= maxHealth) return false;
        return _timeSinceLastCombatDamage >= RegenDelays[_upgradeNode];
    }

    /// <summary>
    /// Diagnostic (regen investigation): explains WHY regen is not producing this frame, so a
    /// "health didn't regen" report can be pinned to the exact gate instead of guessed at.
    /// </summary>
    public string GetBlockReason(float currentCombatHealth, float maxHealth)
    {
        if (_regenPaused) return "BLOCKED: regen PAUSED (PauseRegen called, never resumed)";
        if (!_hasEverTakenDamage) return "no regen needed: twin has not taken damage since spawn/restore";
        if (currentCombatHealth >= maxHealth) return "no regen needed: already at max HP";
        if (_timeSinceLastCombatDamage < RegenDelays[_upgradeNode])
            return $"WAITING post-hit delay: {_timeSinceLastCombatDamage:F1}/{RegenDelays[_upgradeNode]:F1}s " +
                   "(a fresh combat hit keeps resetting this to 0)";
        return "ELIGIBLE — should be regenerating (if HP not rising, look upstream of the handler)";
    }

    public float GetRegenThisFrame(float currentCombatHealth, float maxHealth, float deltaTime)
    {
        if (_regenPaused) return 0f;
        if (!_hasEverTakenDamage) return 0f;
        if (currentCombatHealth >= maxHealth) return 0f;

        // BUG FIX 2: timer was advancing even when currentCombatHealth >= maxHealth
        // because the early-exit above came AFTER the increment in the original.
        // Now the increment only happens when we're actually below max and eligible.
        _timeSinceLastCombatDamage += deltaTime;

        if (_timeSinceLastCombatDamage < RegenDelays[_upgradeNode]) return 0f;

        float rate = RegenRates[_upgradeNode] * deltaTime;
        return Mathf.Min(rate, maxHealth - currentCombatHealth);
    }
}