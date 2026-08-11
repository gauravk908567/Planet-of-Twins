using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Join a pile — move to encircle a grabbed twin.
/// GroupGrab only — other enemies achieve pile-on via AttackTwin opportunistic scoring.
/// Hard gates: DoNotRun if target not engaged or possessed.
/// Score driven by UtilityWeightProfile — distance to grabbed twin shapes urgency.
/// </summary>
public class GOAPGoalJoinPile : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        var shared = CommonCore.BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        bool engaged = false;
        shared?.TryGet(PoTNames.TargetIsEngaged, out engaged, false);
        if (!engaged) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}