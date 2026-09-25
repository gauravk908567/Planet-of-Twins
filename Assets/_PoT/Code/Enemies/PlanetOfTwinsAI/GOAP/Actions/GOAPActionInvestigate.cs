using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Investigate sound position.
/// Satisfies GOAPGoalInvestigate.
/// </summary>
public class GOAPActionInvestigate : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalInvestigate) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 5f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("Investigate");
        AddChildToRootNode(root);
        root.AddChild(new BTActionInvestigate());
    }
}