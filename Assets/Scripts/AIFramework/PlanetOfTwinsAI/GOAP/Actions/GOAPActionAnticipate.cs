using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Intercept twin using anticipation.
/// Satisfies GOAPGoalAnticipate.
/// Lower cost than direct attack when twin is far — GOAP prefers it naturally.
/// </summary>
public class GOAPActionAnticipate : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalAnticipate) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 2f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("Anticipate");
        AddChildToRootNode(root);
        root.AddChild(new BTActionAnticipate());
    }
}