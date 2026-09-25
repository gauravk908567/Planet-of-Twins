public interface IAbilityDataStore
{
    AbilityUpgradeData StunData { get; }
    AbilityUpgradeData PossessData { get; }
    AbilityUpgradeData GateData { get; }
    AbilityUpgradeData HealthRegenData { get; }
    AbilityUpgradeData AccordSpiritsData { get; }
    AbilityUpgradeData CoalesceData { get; }
    AbilityUpgradeData SoulConvData { get; }
    AbilityUpgradeData EmpowerData { get; }
    AbilityUpgradeData AccordData { get; }
}