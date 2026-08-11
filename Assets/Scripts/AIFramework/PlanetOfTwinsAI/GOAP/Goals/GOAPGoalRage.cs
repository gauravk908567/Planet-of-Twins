using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Rage chase — full aggro until death.
/// Valid when enemy is in rage state.
/// Priority: Maximum (100) — rage overrides everything.
/// Reusable — any enemy with rage can use this goal.
/// </summary>
public class GOAPGoalRage : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool inRage = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyInRage, out inRage, false);

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);

        Priority = (inRage && !isPossessed) ? GoalPriority.Maximum : GoalPriority.DoNotRun;
    }
}