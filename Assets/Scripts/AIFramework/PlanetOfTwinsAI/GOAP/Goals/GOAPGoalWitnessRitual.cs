using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Perform ritual to summon new ally.
/// Hard gates: DoNotRun if ally alive or possessed.
/// Score driven by UtilityWeightProfile — safety and ritual site availability shape urgency.
/// </summary>
public class GOAPGoalWitnessRitual : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool allyAlive = false;
        LinkedBlackboard.TryGet(PoTNames.WitnessAllyAlive, out allyAlive, false);
        if (allyAlive) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}