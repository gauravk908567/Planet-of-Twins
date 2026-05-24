using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Defend spawn point when it is under attack.
/// Hard gates: DoNotRun if possessed, stunned, or spawn not under attack.
/// Score driven by UtilityWeightProfile — proximity to spawn shapes urgency.
/// </summary>
public class GOAPGoalDefendSpawn : UtilityGOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool isStunned = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsStunned, out isStunned, false);
        if (isStunned) { Priority = GoalPriority.DoNotRun; return; }

        bool spawnUnderAttack = false;
        LinkedBlackboard.TryGet(PoTNames.SpawnUnderAttack, out spawnUnderAttack, false);
        if (!spawnUnderAttack) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}