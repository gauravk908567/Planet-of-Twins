/// <summary>
/// GOAP Brain for SiphonEnemy.
///
/// Goals (Components on prefab):
///   GOAPGoalPossessed   — Maximum (100)
///   GOAPGoalDefendSpawn — Critical (90)
///   GOAPGoalAttackTwin  — High (75)
///
/// Actions (Components on prefab):
///   GOAPActionAttackTwinSiphon
///
/// Ghost spawn is fully event-driven via SiphonEnemy.Initialise()
/// and RescueEventController events — no GOAP involvement needed.
/// </summary>
public class GOAPBrainSiphonEnemy : PoTGOAPBrainBase
{
    private SiphonEnemy _siphon;

    protected override void OnPreTickBrain(float InDeltaTime)
    {
      // UnityEngine.Debug.Log($"[SiphonBrain] ticking — enemy={_siphon?.name}");
    }
    protected override void OnConfigureBrain()
    {
        _siphon = GetComponent<SiphonEnemy>();
        if (_siphon == null)
            UnityEngine.Debug.LogError("[GOAPBrainSiphonEnemy] No SiphonEnemy component.", this);
    }
}