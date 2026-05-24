using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Throw chain at twin.
/// Valid when: has target, in chain range, not on cooldown, not in rage, not throwing.
/// Priority: Critical (90) — chain attack overrides normal attack.
/// </summary>
public class GOAPGoalChainAttack : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target == null) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool inRage = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyInRage, out inRage, false);
        if (inRage) { Priority = GoalPriority.DoNotRun; return; }

        bool onCooldown = false;
        LinkedBlackboard.TryGet(PoTNames.ChainOnCooldown, out onCooldown, false);
        if (onCooldown) { Priority = GoalPriority.DoNotRun; return; }

        Priority = GoalPriority.Critical;
    }
}