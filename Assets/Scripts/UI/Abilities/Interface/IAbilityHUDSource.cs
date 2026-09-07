/// <summary>
/// Implemented by AbilityBase (and therefore all abilities) so AbilityHUDController
/// can poll cooldown, charges, and active state without knowing the concrete type.
///
/// SETUP: AbilityBase must implement this interface and expose:
///   public float CooldownProgress � 0 = on cooldown, 1 = ready (derived from internal timer)
///   public bool  IsActive         � true while the ability effect is running
///   public int   CurrentCharges   � current charge count (1 if no charge system)
///   public int   MaxCharges       � max charges (1 if no charge system)
///   public string AbilityName     � display name (can use data.name from the SO asset)
/// </summary>
public interface IAbilityHUDSource
{
    string AbilityName { get; }
    float CooldownProgress { get; } // 0 = on cooldown, 1 = ready
    int CurrentCharges { get; }
    int MaxCharges { get; }
    bool IsActive { get; }

    // ── Border-as-timer HUD (Track D / Option B) ──────────────────────────────
    // The clan-border + liquid-tank driver (BorderFillDriver) reads these so the
    // active-window DRAIN and the charge/hold ramp are surfaced as distinct
    // channels from CooldownProgress. Abilities that don't hold or don't have a
    // timed active window return the "not applicable" defaults noted below.
    float ActiveProgress { get; }   // 0 = just activated (full window left) → 1 = window elapsed. 1 when N/A.
    bool  IsHolding { get; }         // true while a charge/hold-to-activate is in progress
    float HoldProgress { get; }      // 0→1 fraction of the activation hold (0 when not holding / N/A)
}