using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Join pile — move to encircle grabbed player.
/// Reusable — add to any enemy that should pile on.
/// </summary>
public class GOAPActionJoinPile : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalJoinPile) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 5f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("JoinPile");
        AddChildToRootNode(root);
        root.AddChild(new BTActionJoinPile());
    }
}