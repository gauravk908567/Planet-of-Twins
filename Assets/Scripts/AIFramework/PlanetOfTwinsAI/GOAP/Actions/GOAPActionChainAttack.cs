using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Chain attack — throw chain then sprint.
/// BT: chase to chain range → lock → throw → sprint.
/// </summary>
public class GOAPActionChainAttack : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalChainAttack) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("ChainAttack");
        AddChildToRootNode(root);

        // Add sequence to root FIRST
        var chainSeq = new BTFlowNode_Sequence("ThrowChain");
        root.AddChild(chainSeq);  // ← before adding children to chainSeq

        // THEN add children
        chainSeq.AddChild(new BTActionChainAttack());

        // Fallback
        root.AddChild(new BTActionChaseTarget());
    }
}