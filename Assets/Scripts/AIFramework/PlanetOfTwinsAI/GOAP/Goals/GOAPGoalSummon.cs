using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Summon a minion.
/// Hard gates: DoNotRun if no target, possessed, or can't summon.
/// Score driven by UtilityWeightProfile — low health makes Summoner
/// more desperate to summon, high ally count reduces urgency.
/// </summary>
public class GOAPGoalSummon : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target == null) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool canSummon = false;
        LinkedBlackboard.TryGet(PoTNames.CanSummon, out canSummon, false);
        if (!canSummon) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}