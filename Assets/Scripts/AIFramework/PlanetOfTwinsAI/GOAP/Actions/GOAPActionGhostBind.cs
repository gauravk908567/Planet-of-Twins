using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Ghost binds soul.
/// Satisfies GOAPGoalGhostBind.
/// </summary>
public class GOAPActionGhostBind : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalGhostBind) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 5f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("GhostBind");
        AddChildToRootNode(root);
        root.AddChild(new BTActionGhostBind());
    }
}