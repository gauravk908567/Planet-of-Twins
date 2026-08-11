using CommonCore;

/// <summary>
/// GOAP Brain for WitnessEnemy.
///
/// Goals (Components on prefab):
///   GOAPGoalPossessed      — Maximum (100)
///   GOAPGoalWitnessRitual  — Critical (90)
///   GOAPGoalDefendSpawn    — Critical (90)
///   GOAPGoalThrowBomb      — High (75)     ← NEW
///   GOAPGoalShadowAlly     — High (75)
///   GOAPGoalAttackTwin     — High (75)
///
/// Actions (Components on prefab):
///   GOAPActionWitnessRitual
///   GOAPActionThrowBomb    ← NEW
///   GOAPActionShadowAlly
///   GOAPActionAttackTwinMelee
/// </summary>
public class GOAPBrainWitnessEnemy : PoTGOAPBrainBase
{
    private WitnessEnemy _witness;
    private EnemyPOITracker _poiTracker;

    protected override void OnConfigureBrain()
    {
        _witness = GetComponent<WitnessEnemy>();
        _poiTracker = GetComponent<EnemyPOITracker>();

        if (_witness == null)
            UnityEngine.Debug.LogError("[GOAPBrainWitnessEnemy] No WitnessEnemy.", this);
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.WitnessAllyAlive, false);
        LinkedBlackboard.Set(PoTNames.WitnessIsRitualing, false);
        LinkedBlackboard.Set(PoTNames.BombOnCooldown, false);
        LinkedBlackboard.Set(PoTNames.BombReady, false);
        LinkedBlackboard.Set(PoTNames.TwinInBombRange, false);
        LinkedBlackboard.Set(PoTNames.RitualSiteAvailable, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_witness == null) return;

        LinkedBlackboard.Set(PoTNames.WitnessAllyAlive, _witness.AllyIsAlive);
        LinkedBlackboard.Set(PoTNames.WitnessIsRitualing, _witness.IsRitualing);
        LinkedBlackboard.Set(PoTNames.BombOnCooldown, _witness.BombOnCooldown);
        LinkedBlackboard.Set(PoTNames.RitualSiteAvailable, _poiTracker?.HasRitualSites ?? false);

        // Bomb readiness
        bool bombReady = _witness.CanThrowBomb;
        LinkedBlackboard.Set(PoTNames.BombReady, bombReady);

        // Twin in bomb range
        bool twinInRange = false;
        if (bombReady)
        {
            var twin = _witness.GetNearestTwin();
            if (twin != null)
            {
                float triggerRange = _witness.WitnessData?.bombTriggerRange ?? 6f;
                float dist = UnityEngine.Vector3.Distance(
                    _witness.transform.position, twin.transform.position);
                twinInRange = dist <= triggerRange;
            }
        }
        LinkedBlackboard.Set(PoTNames.TwinInBombRange, twinInRange);
    }
}