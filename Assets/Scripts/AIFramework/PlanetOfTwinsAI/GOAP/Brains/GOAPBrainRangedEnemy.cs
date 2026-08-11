/// <summary>
/// GOAP Brain for RangedEnemy.
///
/// Goals (Components on prefab):
///   PoT_GOAPGoal_Possessed    — Maximum (100)
///   PoT_GOAPGoal_DefendSpawn  — Critical (90)
///   PoT_GOAPGoal_AttackTwin   — High (75)
///
/// Actions (Components on prefab):
///   PoT_GOAPAction_AttackTwin_Ranged
///
/// PREFAB SETUP:
///   Same as MeleeEnemy but replace PoT_GOAPAction_AttackTwin
///   with PoT_GOAPAction_AttackTwin_Ranged.
///   RangedEnemy component replaces Enemy component.
/// </summary>
public class GOAPBrainRangedEnemy : PoTGOAPBrainBase
{
    protected override void OnConfigureBrain()
    {
        // Cache typed reference for ranged-specific data
        var ranged = GetComponent<RangedEnemy>();
        if (ranged == null)
            UnityEngine.Debug.LogError("[PoT_GOAPBrain_RangedEnemy] No RangedEnemy component found.", this);
    }
}