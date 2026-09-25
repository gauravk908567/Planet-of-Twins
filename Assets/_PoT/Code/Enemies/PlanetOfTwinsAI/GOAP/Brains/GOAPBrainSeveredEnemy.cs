using CommonCore;

/// <summary>
/// GOAP Brain for SeveredEnemy.
///
/// Goals (Components on prefab):
///   GoalPossessed      — Maximum (100)
///   GoalGriefRage      — Critical (90) — fires when partner dead + grace expired
///   GoalDefendSpawn    — Critical (90)
///   GoalAttackTwin     — High (75)
///
/// Actions (Components on prefab):
///   ActionAttackTwin   — standard chase + attack (reused from melee)
///
/// Extra Blackboard keys synced each frame:
///   PoTNames.EnemyPartnerDead
///   PoTNames.EnemyInGriefRage
/// </summary>
public class GOAPBrainSeveredEnemy : PoTGOAPBrainBase
{
    private SeveredEnemy _severed;

    protected override void OnConfigureBrain()
    {
        _severed = GetComponent<SeveredEnemy>();
        if (_severed == null)
            UnityEngine.Debug.LogError("[GOAPBrainSeveredEnemy] No SeveredEnemy component.", this);
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.EnemyPartnerDead, false);
        LinkedBlackboard.Set(PoTNames.EnemyInGriefRage, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_severed == null) return;

        LinkedBlackboard.Set(PoTNames.EnemyPartnerDead, _severed.PartnerDead);
        LinkedBlackboard.Set(PoTNames.EnemyInGriefRage, _severed.IsInGriefRage);
    }
}