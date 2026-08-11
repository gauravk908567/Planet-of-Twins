/// <summary>
/// Implemented by AbilityBase (and therefore all abilities) so AbilityHUDController
/// can poll cooldown, charges, and active state without knowing the concrete type.
///
/// SETUP: AbilityBase must implement this interface and expose:
///   public float CooldownProgress — 0 = on cooldown, 1 = ready (derived from internal timer)
///   public bool  IsActive         — true while the ability effect is running
///   public int   CurrentCharges   — current charge count (1 if no charge system)
///   public int   MaxCharges       — max charges (1 if no charge system)
///   public string AbilityName     — display name (can use data.name from the SO asset)
/// </summary>
public interface IAbilityHUDSource
{
    string AbilityName { get; }
    float CooldownProgress { get; } // 0 = on cooldown, 1 = ready
    int CurrentCharges { get; }
    int MaxCharges { get; }
    bool IsActive { get; }
}