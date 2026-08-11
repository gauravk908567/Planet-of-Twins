using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Intercept twin using anticipation.
/// Valid when: has target AND twin is moving AND enemy is far enough.
/// Priority: High (75) — same as AttackTwin, selected by GOAP cost.
///           GOAPActionAnticipate has lower cost than direct attack
///           when twin is far — GOAP picks anticipation naturally.
/// </summary>
public class GOAPGoalAnticipate : GOAPGoalBase
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

        // Check if anticipation available
        var anticipator = target.GetComponent<TwinAnticipator>();

        //Debug.Log($"[GOAPAnticipate] {gameObject.name} target={target.name} " +
        //      $"anticipator={anticipator != null} isMoving={anticipator?.IsMoving}");

        if (anticipator == null || !anticipator.IsMoving)
        { Priority = GoalPriority.DoNotRun; return; }

        Priority = GoalPriority.High;
    }
}