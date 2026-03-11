public interface IAbilityDataStore
{
    AbilityUpgradeData StunData { get; }
    AbilityUpgradeData PossessData { get; }
    AbilityUpgradeData GateData { get; }
    AbilityUpgradeData HealthRegenData { get; }
    AbilityUpgradeData DualCastData { get; }
    AbilityUpgradeData CoalesceData { get; }
    AbilityUpgradeData SoulConvData { get; }
}