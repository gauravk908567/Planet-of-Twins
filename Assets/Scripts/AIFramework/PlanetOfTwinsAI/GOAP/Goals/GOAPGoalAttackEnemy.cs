using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Attack nearest enemy of opposite clan during clan war.
/// Hard gates: DoNotRun if possessed, clan war not active, or twin in threat range.
/// Priority scales with clan war intensity — Medium(50) to High(75).
/// When twin enters twinThreatRange this goal drops below GOAPGoalAttackTwin.
/// </summary>
public class GOAPGoalAttackEnemy : GOAPGoalBase
{
    public override void PrepareForPlanning()
    {
        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        bool clanWarActive = false;
        LinkedBlackboard.TryGet(PoTNames.ClanWarActive, out clanWarActive, false);
        if (!clanWarActive) { Priority = GoalPriority.DoNotRun; return; }

        bool twinInRange = false;
        LinkedBlackboard.TryGet(PoTNames.TwinInDangerRange, out twinInRange, false);
        if (twinInRange) { Priority = GoalPriority.DoNotRun; return; }

        float intensity = 0f;
        LinkedBlackboard.TryGet(PoTNames.ClanWarIntensity, out intensity, 0f);
        Priority = (int)Mathf.Lerp(50f, 75f, intensity);
    }
}