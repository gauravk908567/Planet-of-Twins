using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Stay within PactRange of nearest pact member.
/// Hard gates: DoNotRun if possessed, stunned, not regrouping,
///             or actively chasing twin.
/// Score driven by UtilityWeightProfile — partner distance shapes urgency.
/// </summary>
public class GOAPGoalStayInPact : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        // Hard gates
        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool isStunned = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsStunned, out isStunned, false);
        if (isStunned) { Priority = GoalPriority.DoNotRun; return; }

        bool needsRegroup = false;
        LinkedBlackboard.TryGet(PoTNames.IsRallying, out needsRegroup, false);
        if (!needsRegroup) { Priority = GoalPriority.DoNotRun; return; }

        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target != null) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}