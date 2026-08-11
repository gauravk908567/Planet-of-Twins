using System;

public interface ISkillTreePurchaser
{
    bool CanAfford(AbilityUpgradeData data);
    bool TryPurchaseNode(AbilityUpgradeData data);

    event Action<AbilityUpgradeData> OnNodePurchased;
}