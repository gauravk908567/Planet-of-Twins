using CommonCore;

/// <summary>
/// GOAP Brain for PenitentEnemy.
///
/// Goals (Components on prefab):
///   GOAPGoalPossessed   — Maximum (100)
///   GOAPGoalDefendSpawn — Critical (90)
///   GOAPGoalAttackTwin  — High (75)
///
/// Actions (Components on prefab):
///   GOAPActionAttackTwinPenitent
///
/// All grab/reflection/rage logic is self-contained in PenitentEnemy.
/// Brain only syncs state flags to Blackboard for goal reading.
/// </summary>
public class GOAPBrainPenitentEnemy : PoTGOAPBrainBase
{
    private PenitentEnemy _penitent;

    protected override void OnConfigureBrain()
    {
        _penitent = GetComponent<PenitentEnemy>();
        if (_penitent == null)
            UnityEngine.Debug.LogError("[GOAPBrainPenitentEnemy] No PenitentEnemy component.", this);
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.EnemyIsGrabbing, false);
        LinkedBlackboard.Set(PoTNames.ReflectionActive, false);
        LinkedBlackboard.Set(PoTNames.EnemyInRage, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        LinkedBlackboard.Set(PoTNames.EnemyIsGrabbing, _penitent.IsCrushing);
        LinkedBlackboard.Set(PoTNames.ReflectionActive, _penitent.ReflectionActive);
        LinkedBlackboard.Set(PoTNames.EnemyInRage, _penitent.IsInRage);
        LinkedBlackboard.Set(PoTNames.EnemyIsStunned, _penitent.IsWindingUp || _penitent.IsInCooldown);
    }
}