using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Search at last known twin position.
/// Satisfies GOAPGoalSearch.
/// </summary>
public class GOAPActionSearch : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalSearch) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 5f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("Search");
        AddChildToRootNode(root);
        root.AddChild(new BTActionInvestigate()); // ← try investigate first
        root.AddChild(new BTActionSearch());
    }
}