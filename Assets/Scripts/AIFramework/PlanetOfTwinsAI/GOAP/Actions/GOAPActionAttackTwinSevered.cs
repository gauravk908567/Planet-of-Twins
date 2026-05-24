using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Severed enemy attack.
/// Satisfies both GOAPGoalAttackTwin and GOAPGoalGriefRage.
/// Rage speed applied by SeveredEnemy.EnterGriefRage() directly.
/// All values read from SeveredEnemyData SO at runtime — no serialized fields.
/// </summary>
public class GOAPActionAttackTwinSevered : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[]
        {
            typeof(GOAPGoalAttackTwin),
            typeof(GOAPGoalGriefRage)
        };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("AttackTwin_Severed");
        AddChildToRootNode(root);
        root.AddChild(new BTActionAttack());
        root.AddChild(new BTActionChaseTarget());
    }
}