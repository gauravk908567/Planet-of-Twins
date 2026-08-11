using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Execute combo attack with pact members.
/// Valid when: in pact, combo power ready, has target.
/// Priority: High — fires when combo conditions met.
/// </summary>
public class GOAPGoalComboAttack : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool isStunned = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsStunned, out isStunned, false);
        if (isStunned) { Priority = GoalPriority.DoNotRun; return; }

        // Need target
        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target == null) { Priority = GoalPriority.DoNotRun; return; }

        // Combo must be ready
        bool comboReady = false;
        LinkedBlackboard.TryGet(PoTNames.ComboReady, out comboReady, false);
        if (!comboReady) { Priority = GoalPriority.DoNotRun; return; }

        Priority = GoalPriority.Critical;
    }
}