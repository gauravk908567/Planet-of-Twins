using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Attack nearest enemy of opposite clan during clan war.
/// Satisfies GOAPGoalAttackEnemy.
/// Reuses BTActionAttackEnemy — same scan and chase logic as possessed targeting.
/// </summary>
public class GOAPActionAttackEnemy : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalAttackEnemy) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("AttackEnemy");
        AddChildToRootNode(root);
        root.AddChild(new BTActionAttackEnemy());
    }
}