/// <summary>
/// GOAP Brain for BasicMeleeEnemy.
///
/// Goals (added as Components on the prefab):
///   PoT_GOAPGoal_Possessed    — Priority: Maximum (100) — when possessed
///   PoT_GOAPGoal_DefendSpawn  — Priority: Critical (90) — when spawn attacked
///   PoT_GOAPGoal_AttackTwin   — Priority: High     (75) — default combat
///
/// Actions (added as Components on the prefab):
///   PoT_GOAPAction_AttackTwin — satisfies AttackTwin goal
///
/// PREFAB SETUP:
///   1. Add this component to the enemy prefab
///   2. Add PoT_GOAPGoal_Possessed as Component
///   3. Add PoT_GOAPGoal_DefendSpawn as Component
///   4. Add PoT_GOAPGoal_AttackTwin as Component
///   5. Add PoT_GOAPAction_AttackTwin as Component — set Attack Range in Inspector
///   6. Add PerceptionListener as Component — wire SensorConfigs and Faction
///   7. Add FactionComponent as Component — assign Faction_AI asset
///
/// The brain finds all goals and actions automatically via GetComponents in GOAPBrainBase.
/// No manual wiring of goals/actions needed beyond adding them as Components.
/// </summary>
public class GOAPBrainMeleeEnemy : PoTGOAPBrainBase
{
    // BasicMeleeEnemy has no additional blackboard keys or references.
    // Goals and actions are Components — GOAPBrainBase finds them automatically.
    // Override OnConfigureBrain() here if you need to cache enemy-specific refs.
}