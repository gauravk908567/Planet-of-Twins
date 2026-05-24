using CommonCore;

/// <summary>
/// GOAP Brain for TetherBreakerEnemy.
///
/// Goals (Components on prefab):
///   GOAPGoalPossessed    — Maximum (100)
///   GOAPGoalRage         — Maximum (100) — rage after chain broken
///   GOAPGoalChainAttack  — Critical (90) — throw chain
///   GOAPGoalDefendSpawn  — Critical (90)
///   GOAPGoalAttackTwin   — High (75)     — fallback chase
///
/// Actions (Components on prefab):
///   GOAPActionRageAttack
///   GOAPActionChainAttack
///   GOAPActionAttackTwinMelee
/// </summary>
public class GOAPBrainTetherBreakerEnemy : PoTGOAPBrainBase
{
    private TetherBreakerEnemy _tbEnemy;

    protected override void OnConfigureBrain()
    {
        _tbEnemy = GetComponent<TetherBreakerEnemy>();
        if (_tbEnemy == null)
            UnityEngine.Debug.LogError("[GOAPBrainTetherBreakerEnemy] No TetherBreakerEnemy component.", this);
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.EnemyInRage, false);
        LinkedBlackboard.Set(PoTNames.ChainOnCooldown, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_tbEnemy == null) return;
        LinkedBlackboard.Set(PoTNames.EnemyInRage, _tbEnemy.IsInRage);
        LinkedBlackboard.Set(PoTNames.ChainOnCooldown, _tbEnemy.ChainOnCooldown);
    }
}