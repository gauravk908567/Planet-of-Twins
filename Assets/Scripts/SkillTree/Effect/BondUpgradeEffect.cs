using UnityEngine;

public class BondUpgradeEffect : ISkillEffect
{
    private TwinBondManager bondManager;
    private int upgradeIncrease;

    public BondUpgradeEffect(
        TwinBondManager manager,
        int amount)
    {
        bondManager = manager;
        upgradeIncrease = amount;
    }

    public void Apply()
    {
        bondManager.IncreaseUpgradeLevel(upgradeIncrease);
    }
}