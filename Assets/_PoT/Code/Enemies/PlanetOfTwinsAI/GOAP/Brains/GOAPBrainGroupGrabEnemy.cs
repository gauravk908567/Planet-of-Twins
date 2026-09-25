using CommonCore;

/// <summary>
/// GOAP Brain for GroupGrabEnemy.
///
/// Goals (Components on prefab):
///   GOAPGoalPossessed   — Maximum (100)
///   GOAPGoalDefendSpawn — Critical (90)
///   GOAPGoalGrabTwin    — Critical (90) — get behind and grab
///   GOAPGoalAttackTwin  — High (75)     — fallback front attack
///
/// Actions (Components on prefab):
///   GOAPActionGrabTwin
///   GOAPActionAttackTwinMelee  — fallback
/// </summary>
public class GOAPBrainGroupGrabEnemy : PoTGOAPBrainBase
{
    private GroupGrabEnemy _grabEnemy;

    protected override void OnConfigureBrain()
    {
        _grabEnemy = GetComponent<GroupGrabEnemy>();
        if (_grabEnemy == null)
            UnityEngine.Debug.LogError("[GOAPBrainGroupGrabEnemy] No GroupGrabEnemy component.", this);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_grabEnemy == null) return;
        LinkedBlackboard.Set(PoTNames.EnemyIsGrabbing, _grabEnemy.IsGrabbing);
    }
}