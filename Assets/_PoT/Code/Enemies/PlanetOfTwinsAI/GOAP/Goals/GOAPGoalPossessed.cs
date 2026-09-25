using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Act as a possessed enemy — attack other enemies.
///
/// Valid when:
///   - Enemy is currently possessed by Lyra
///
/// Priority: Urgent — possession overrides everything else.
/// When possessed the enemy turns on its allies.
/// The BT action for this goal handles targeting and attacking other enemies.
/// </summary>
public class GOAPGoalPossessed : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);

        Priority = isPossessed ? GoalPriority.Maximum : GoalPriority.DoNotRun;
    }
}