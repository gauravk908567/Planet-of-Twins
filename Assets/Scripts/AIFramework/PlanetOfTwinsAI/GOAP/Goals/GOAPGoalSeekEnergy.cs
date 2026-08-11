using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: idle-visit the nearest energy-feeding POI (PoiEnergyEmitter) and stand in its radius to
/// be fed dark energy + health. The ecology beat: enemies drift to ritual sites when nothing is
/// happening; a bond-broken (corrupted) enemy seeks far more often, a hurt one seeks harder.
/// Hard gates: DoNotRun with a target, with perception memory, possessed, stunned, or when no
/// feed site exists. Base score from the UtilityWeightProfile (SeekEnergyUtilProfile).
/// </summary>
public class GOAPGoalSeekEnergy : UtilityGOAPGoalBase
{
    [Tooltip("Flat score bonus once the bond is broken — corrupted enemies visit feed sites far more often.")]
    [SerializeField] private float _bondBrokenBonus = 25f;

    [Tooltip("Flat score bonus while below half health — hurt enemies go recharge.")]
    [SerializeField] private float _lowHealthBonus = 15f;

    public override void PrepareForPlanning()
    {
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target != null) { Priority = GoalPriority.DoNotRun; return; }

        bool hasMemory = false;
        LinkedBlackboard.TryGet(PoTNames.HasPerceptionMemory, out hasMemory, false);
        if (hasMemory) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool isStunned = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsStunned, out isStunned, false);
        if (isStunned) { Priority = GoalPriority.DoNotRun; return; }

        if (PoiEnergyEmitter.FindNearest(transform.position) == null)
        { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }

    protected override float OnAdditionalScore()
    {
        float bonus = 0f;

        bool bondBroken = false;
        LinkedBlackboard.TryGet(PoTNames.BondBroken, out bondBroken, false);
        if (bondBroken) bonus += _bondBrokenBonus;

        float healthNorm = 1f;
        LinkedBlackboard.TryGet(PoTNames.EnemyHealthNorm, out healthNorm, 1f);
        if (healthNorm < 0.5f) bonus += _lowHealthBonus;

        return bonus;
    }
}
