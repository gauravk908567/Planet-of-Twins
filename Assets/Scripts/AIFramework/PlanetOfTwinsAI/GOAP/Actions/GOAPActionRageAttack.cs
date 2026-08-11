using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Rage attack — sprint at full aggro, attack on reach.
/// Satisfies GOAPGoalRage.
/// </summary>
public class GOAPActionRageAttack : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalRage) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("RageAttack");
        AddChildToRootNode(root);

        root.AddChild(new BTActionAttack());
        root.AddChild(new BTActionRageChase());
    }
}