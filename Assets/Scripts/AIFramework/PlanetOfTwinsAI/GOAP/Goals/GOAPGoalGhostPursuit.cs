using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Pursue soul — default ghost state.
/// Valid when not binding, not retreating, soul not yet in range.
/// Priority: High (75).
/// </summary>
public class GOAPGoalGhostPursuit : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isBinding = false;
        bool isRetreating = false;
        bool soulInRange = false;

        LinkedBlackboard.TryGet(PoTNames.GhostIsBinding, out isBinding, false);
        LinkedBlackboard.TryGet(PoTNames.GhostIsRetreating, out isRetreating, false);
        LinkedBlackboard.TryGet(PoTNames.GhostSoulInRange, out soulInRange, false);

        Priority = (!isBinding && !isRetreating && !soulInRange)
            ? GoalPriority.High
            : GoalPriority.DoNotRun;
    }
}