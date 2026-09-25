using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Defend spawn point when it is under attack.
/// Hard gates: DoNotRun if possessed, stunned, rescue active, or spawn not under attack.
/// Score driven by UtilityWeightProfile � proximity to spawn shapes urgency.
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

        // BUG-083 — during a rescue, enemies pause hostile/spawn-defence behaviour so the twins can
        // free the soul. Mirrors the gate on GOAPGoalAttackTwin/GOAPGoalGrabTwin. Without it, DefendSpawn
        // (priority 90) out-ranked the rescue-gated AttackTwin (75) and kept the enemy active during a
        // rescue whenever the global SpawnUnderAttack flag was set (e.g. a summoner's spawn point under attack).
        bool rescueActive = false;
        LinkedBlackboard.TryGet(PoTNames.IsRescueActive, out rescueActive, false);
        if (rescueActive) { Priority = GoalPriority.DoNotRun; return; }

        bool spawnUnderAttack = false;
        LinkedBlackboard.TryGet(PoTNames.SpawnUnderAttack, out spawnUnderAttack, false);
        if (!spawnUnderAttack) { Priority = GoalPriority.DoNotRun; return; }

        base.PrepareForPlanning();
    }
}