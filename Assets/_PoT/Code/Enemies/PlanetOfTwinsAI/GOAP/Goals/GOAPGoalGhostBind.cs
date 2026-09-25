using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Bind soul — fires when ghost reaches soul.
/// Valid when soul is in range and ghost is not already binding or retreating.
/// Priority: Critical (90) — preempts pursuit immediately.
/// </summary>
public class GOAPGoalGhostBind : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isBinding = false;
        bool isRetreating = false;
        bool soulInRange = false;

        LinkedBlackboard.TryGet(PoTNames.GhostIsBinding, out isBinding, false);
        LinkedBlackboard.TryGet(PoTNames.GhostIsRetreating, out isRetreating, false);
        LinkedBlackboard.TryGet(PoTNames.GhostSoulInRange, out soulInRange, false);

        Priority = (soulInRange && !isBinding && !isRetreating)
            ? GoalPriority.Critical
            : GoalPriority.DoNotRun;
    }
}