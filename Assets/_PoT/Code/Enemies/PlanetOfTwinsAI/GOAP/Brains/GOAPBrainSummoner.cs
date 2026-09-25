using CommonCore;

/// <summary>
/// GOAP Brain for SummonerEnemy.
///
/// Goals (Components on prefab):
///   GOAPGoalPossessed   — Maximum (100)
///   GOAPGoalSummon      — Critical (90) — summon when ready
///   GOAPGoalDefendSpawn — Critical (90)
///   GOAPGoalAttackTwin  — High (75)     — ranged attack fallback
///
/// Actions (Components on prefab):
///   GOAPActionSummon
///   GOAPActionAttackTwinRanged  ← reused from RangedEnemy
/// </summary>
public class GOAPBrainSummonerEnemy : PoTGOAPBrainBase
{
    private SummonerEnemy _summoner;

    protected override void OnConfigureBrain()
    {
        _summoner = GetComponent<SummonerEnemy>();
        if (_summoner == null)
            UnityEngine.Debug.LogError("[GOAPBrainSummonerEnemy] No SummonerEnemy component.", this);
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.CanSummon, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_summoner == null) return;
        LinkedBlackboard.Set(PoTNames.CanSummon, _summoner.CanSummon);
    }
}