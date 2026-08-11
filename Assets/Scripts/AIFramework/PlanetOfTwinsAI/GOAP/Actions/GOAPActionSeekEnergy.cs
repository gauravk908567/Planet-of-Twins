using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: walk to the nearest energy-feeding POI and stand in its feed radius.
/// Satisfies GOAPGoalSeekEnergy.
/// </summary>
public class GOAPActionSeekEnergy : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalSeekEnergy) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs;

    public override float CalculateCost() => 4f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("SeekEnergy");
        AddChildToRootNode(root);
        root.AddChild(new BTActionSeekEnergy());
    }
}
