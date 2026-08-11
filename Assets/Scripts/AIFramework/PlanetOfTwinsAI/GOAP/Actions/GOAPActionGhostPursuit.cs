using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Ghost pursues soul.
/// Satisfies GOAPGoalGhostPursuit.
/// </summary>
public class GOAPActionGhostPursuit : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalGhostPursuit) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("GhostPursuit");
        AddChildToRootNode(root);
        root.AddChild(new BTActionGhostPursuit());
    }
}