using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Search for twin after losing sight.
/// Hard gates: DoNotRun if actively seeing twin, no memory, possessed,
///             or already investigating a sound.
/// Score driven by UtilityWeightProfile — confidence level shapes urgency.
/// </summary>
public class GOAPGoalSearch : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        // Hard gates
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target != null) { Priority = GoalPriority.DoNotRun; return; }

        bool hasMemory = false;
        LinkedBlackboard.TryGet(PoTNames.HasPerceptionMemory, out hasMemory, false);
        if (!hasMemory) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        float confidence = 0f;
        LinkedBlackboard.TryGet(PoTNames.PerceptionConfidence, out confidence, 0f);
        if (confidence <= 0.05f) { Priority = GoalPriority.DoNotRun; return; }

        int searchState = 0;
        LinkedBlackboard.TryGet(PoTNames.EnemySearchState, out searchState, 0);
        if ((EnemySearchState)searchState == EnemySearchState.Investigating)
        { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}