using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Wander randomly when no target and no memory.
/// Hard gates: DoNotRun if target detected, has memory, or possessed.
/// Score driven by UtilityWeightProfile — fires only when nothing else scores higher.
/// </summary>
public class GOAPGoalWander : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        // Hard gates — these block completely regardless of score
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target != null) { Priority = GoalPriority.DoNotRun; return; }

        bool hasMemory = false;
        LinkedBlackboard.TryGet(PoTNames.HasPerceptionMemory, out hasMemory, false);
        if (hasMemory) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        // Pass to utility scorer
        base.PrepareForPlanning();
    }
}