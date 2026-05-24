using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Hold formation slot position relative to commander.
/// Satisfies GOAPGoalHoldFormation.
/// </summary>
public class GOAPActionHoldFormation : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalHoldFormation) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 8f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("HoldFormation");
        AddChildToRootNode(root);
        root.AddChild(new BTActionHoldFormation());
    }
}