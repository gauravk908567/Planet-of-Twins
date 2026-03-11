using UnityEngine;

public class HealthRegenHandler
{
    // GDD §6.5: Rate(n) = 16.6 × (4/3)^n
    private static readonly float[] RegenRates = { 16.6f, 22.1f, 27.7f, 33.3f };
    private static readonly float[] RegenDelays = { 6.0f, 4.51f, 3.61f, 3.0f };

    private int _upgradeNode;
    private float _timeSinceLastCombatDamage;
    private bool _regenPaused;

    public void SetUpgradeNode(int node)
    {
        _upgradeNode = Mathf.Clamp(node, 0, RegenRates.Length - 1);
    }

    /// <summary>
    /// Call this when the player takes COMBAT damage.
    /// Environmental damage (>18u drain) must NOT reset the timer — pass
    /// DamageType.Environmental and this method is skipped by the caller.
    /// </summary>
    public void OnCombatDamageTaken()
    {
        _timeSinceLastCombatDamage = 0f;
    }

    public void PauseRegen() => _regenPaused = true;
    public void ResumeRegen() => _regenPaused = false;

    /// <summary>
    /// Returns the amount of health to restore this frame (may be 0).
    /// Caller adds this to currentCombatHealth.
    /// </summary>
    public float GetRegenThisFrame(float currentCombatHealth, float maxHealth, float deltaTime)
    {
        if (_regenPaused) return 0f;
        if (currentCombatHealth >= maxHealth) return 0f;
        _timeSinceLastCombatDamage += deltaTime;
        if (_timeSinceLastCombatDamage < RegenDelays[_upgradeNode]) return 0f;

        float rate = RegenRates[_upgradeNode] * deltaTime;
        return Mathf.Min(rate, maxHealth - currentCombatHealth);
    }
}

