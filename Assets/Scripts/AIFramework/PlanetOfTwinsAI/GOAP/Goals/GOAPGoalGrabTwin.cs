using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Grab the nearest twin from behind.
/// Valid when:
///   - Has a perceived target
///   - Not already grabbing
///   - Not on grab cooldown
///   - Rescue not active
///   - Not possessed
/// Priority: Critical — grab overrides normal attack.
/// </summary>
public class GOAPGoalGrabTwin : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target == null) { Priority = GoalPriority.DoNotRun; return; }

        bool rescueActive = false;
        LinkedBlackboard.TryGet(PoTNames.IsRescueActive, out rescueActive, false);
        if (rescueActive) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool isGrabbing = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsGrabbing, out isGrabbing, false);
        if (isGrabbing) { Priority = GoalPriority.DoNotRun; return; }

        Priority = GoalPriority.Critical;
    }
}