using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Coordinated rush during faction energy burst.
/// Valid ONLY during an energy burst (faction energy > 90).
/// Priority: Maximum (100) — overrides everything including possession.
/// Duration: 10 seconds, then energy drops to 60 and burst cools down.
///
/// All enemies with this component rush simultaneously.
/// Creates a memorable coordinated threat moment.
/// </summary>
public class GOAPGoalEnergyBurst : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool burstActive = false;
        var shared = CommonCore.BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        shared?.TryGet(PoTNames.EnergyBurstActive, out burstActive, false);

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);

        // Possessed enemies don't participate in faction burst
        Priority = (burstActive && !isPossessed)
            ? GoalPriority.Maximum
            : GoalPriority.DoNotRun;
    }
}