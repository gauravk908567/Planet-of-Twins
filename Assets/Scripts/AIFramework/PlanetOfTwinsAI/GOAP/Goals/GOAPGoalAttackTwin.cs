using CommonCore;
using UnityEngine;
using HybridGOAP;

/// <summary>
/// Goal: Attack the nearest detected twin.
///
/// Hard gates: DoNotRun if no target, rescue active, or possessed.
/// Score driven by UtilityWeightProfile — context shapes attack eagerness.
/// Each enemy type assigns its own profile SO for different attack personalities.
/// </summary>
public class GOAPGoalAttackTwin : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        // Hard gates
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target == null) { Priority = GoalPriority.DoNotRun; return; }

        bool rescueActive = false;
        LinkedBlackboard.TryGet(PoTNames.IsRescueActive, out rescueActive, false);
        if (rescueActive) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}