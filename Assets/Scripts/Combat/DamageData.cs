using UnityEngine;

public enum DamageType
{
    /// <summary>
    /// Caused by an enemy attack. Resets the health regen timer.
    /// </summary>
    Combat,

    /// <summary>
    /// Caused by a player ability (Coalesce aura, future ability damage etc.).
    /// Counts as a player kill for Soul Convergence but is distinct from direct
    /// melee/ranged Combat so systems can differentiate if needed.
    /// </summary>
    Ability,

    /// <summary>
    /// Caused by the >18u distance drain. Does NOT reset the regen timer —
    /// a player shouldn't lose their regen window just from drifting.
    /// </summary>
    Environmental,

    /// <summary>
    /// Damage transferred between linked enemies (e.g. Severed pair).
    /// Does NOT reset regen timer. Skipped by linked-damage handlers to prevent
    /// infinite loops. Never triggers kill credit or Accord bar fill.
    /// </summary>
    LinkedDamage
}

/// <summary>
/// Single authoritative damage payload for the entire project.
/// Constructed by attack strategies; consumed by IDamageable implementors.
/// </summary>
public readonly struct DamageData
{
    public readonly float Amount;
    public readonly DamageType Type;
    public readonly GameObject Source;    // who dealt the damage (for aggro, knockback)
    public readonly Vector3 HitPoint;  // world-space impact position (for VFX, audio)

    // ── Constructors ───────────────────────────────────────────
    /// <summary>Full constructor — use for all enemy melee/ranged attacks.</summary>
    public DamageData(float amount, DamageType type, GameObject source, Vector3 hitPoint)
    {
        Amount = amount;
        Type = type;
        Source = source;
        HitPoint = hitPoint;
    }

    /// <summary>
    /// Convenience constructor for environmental damage (no physical source or hit point).
    /// Used exclusively by Over18DrainCalculator.
    /// </summary>
    public DamageData(float amount, DamageType type)
    {
        Amount = amount;
        Type = type;
        Source = null;
        HitPoint = Vector3.zero;
    }
}