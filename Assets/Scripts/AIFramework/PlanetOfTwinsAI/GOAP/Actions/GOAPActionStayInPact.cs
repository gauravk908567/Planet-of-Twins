using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Move toward nearest pact member when too far.
/// Satisfies GOAPGoalStayInPact.
/// </summary>
public class GOAPActionStayInPact : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalStayInPact) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 3f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("StayInPact");
        AddChildToRootNode(root);
        root.AddChild(new BTActionStayInPact());
    }
}