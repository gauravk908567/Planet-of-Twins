using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Shadow the summoned ally.
/// Hard gates: DoNotRun if ally dead, possessed, or ritualing.
/// Score driven by UtilityWeightProfile — ally health and threat proximity shape urgency.
/// </summary>
public class GOAPGoalShadowAlly : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool allyAlive = false;
        LinkedBlackboard.TryGet(PoTNames.WitnessAllyAlive, out allyAlive, false);
        if (!allyAlive) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool isRitualing = false;
        LinkedBlackboard.TryGet(PoTNames.WitnessIsRitualing, out isRitualing, false);
        if (isRitualing) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}