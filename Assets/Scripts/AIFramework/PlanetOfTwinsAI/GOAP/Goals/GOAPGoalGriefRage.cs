using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Enter grief rage after partner dies and grace window expires.
///
/// Valid when:
///   - Partner is dead (EnemyPartnerDead = true)
///   - Grace window has expired (EnemyInGriefRage = true)
///   - Enemy is not possessed
///
/// Priority: Critical (90) — overrides normal attack and defend spawn.
/// Rage attack uses faster cooldowns — same BTActionAttack, different stats.
/// </summary>
public class GOAPGoalGriefRage : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool inRage = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyInGriefRage, out inRage, false);

       // UnityEngine.Debug.Log($"[GOAPGoalGriefRage] inRage={inRage}");

        if (!inRage)
        {
            Priority = GoalPriority.DoNotRun;
            return;
        }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed)
        {
            Priority = GoalPriority.DoNotRun;
            return;
        }

        Priority = GoalPriority.Critical;
    }
}