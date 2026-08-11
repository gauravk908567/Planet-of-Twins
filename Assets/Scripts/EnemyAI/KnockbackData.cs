using UnityEngine;

/// <summary>
/// Carries force and source context for a knockback event.
/// Source lets individual enemies decide whether to accept or block
/// based on which ability triggered it � same pattern as DamageType on DamageData.
/// </summary>
public struct KnockbackData
{
    public Vector3 Force;
    public KnockbackSource Source;

    public KnockbackData(Vector3 force, KnockbackSource source)
    {
        Force = force;
        Source = source;
    }
}

public enum KnockbackSource
{
    Empower,        // EmpowerSystem pulse
    AccordState,    // X bar Accord State abilities
    AccordSpirits,  // Dual Cast ancient warrior spirits
    Environmental,  // anything else / default
    Bomb            // BombProjectile explosion pushback
}