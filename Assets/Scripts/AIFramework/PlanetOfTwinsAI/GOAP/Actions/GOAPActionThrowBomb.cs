using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Throw bomb then retreat.
/// Satisfies GOAPGoalThrowBomb.
/// BT: throw bomb → retreat away from twin.
/// </summary>
public class GOAPActionThrowBomb : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalThrowBomb) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 5f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Sequence("ThrowBomb");
        AddChildToRootNode(root);
        root.AddChild(new BTActionThrowBomb());
        root.AddChild(new BTActionWitnessRetreat());
    }
}