using CommonCore;
using HybridGOAP;

/// <summary>
/// Goal: Throw bomb when twin enters trigger range.
/// Reusable — any enemy with a bomb can use this.
/// Reads bombTriggerRange from WitnessEnemyData.
/// Priority: High — defensive, fires when twin too close.
/// </summary>
public class GOAPGoalThrowBomb : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool bombReady = false;
        LinkedBlackboard.TryGet(PoTNames.BombReady, out bombReady, false);
        if (!bombReady) { Priority = GoalPriority.DoNotRun; return; }

        bool twinInRange = false;
        LinkedBlackboard.TryGet(PoTNames.TwinInBombRange, out twinInRange, false);
        if (!twinInRange) { Priority = GoalPriority.DoNotRun; return; }

        Priority = GoalPriority.Critical;
    }
}