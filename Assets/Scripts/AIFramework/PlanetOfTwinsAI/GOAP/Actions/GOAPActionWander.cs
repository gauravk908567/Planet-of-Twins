using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Wander randomly when idle.
/// Satisfies GOAPGoalWander.
/// </summary>
public class GOAPActionWander : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalWander) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 1f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("Wander");
        AddChildToRootNode(root);
        root.AddChild(new BTActionWander());
    }
}