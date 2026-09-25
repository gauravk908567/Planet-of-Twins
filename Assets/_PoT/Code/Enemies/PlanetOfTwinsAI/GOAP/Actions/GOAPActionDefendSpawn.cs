using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Rush to defend spawn point.
/// Satisfies GOAPGoalDefendSpawn.
/// </summary>
public class GOAPActionDefendSpawn : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalDefendSpawn) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 2f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("DefendSpawn");
        AddChildToRootNode(root);
        root.AddChild(new BTActionDefendSpawn());
    }
}