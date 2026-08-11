using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Investigate a sound event position.
/// Hard gates: DoNotRun if actively seeing twin, not in Investigating state, possessed.
/// Score driven by UtilityWeightProfile — sound proximity shapes urgency.
/// </summary>
public class GOAPGoalInvestigate : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        // Hard gates
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target != null) { Priority = GoalPriority.DoNotRun; return; }

        int searchState = 0;
        LinkedBlackboard.TryGet(PoTNames.EnemySearchState, out searchState, 0);
        if ((EnemySearchState)searchState != EnemySearchState.Investigating)
        { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}