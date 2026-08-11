using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Execute the grab behaviour.
/// BT: Get behind → wait behind → grab.
/// Fallback: front attack if can't get behind.
/// </summary>
public class GOAPActionGrabTwin : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalGrabTwin) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("GrabTwin");
        AddChildToRootNode(root);

        // Add sequence to root FIRST so OwningTree propagates
        var grabSeq = new BTFlowNode_Sequence("GrabSequence");
        root.AddChild(grabSeq);  // ← add to root before adding children

        // NOW add children to sequence
        grabSeq.AddChild(new BTActionGetBehindTarget());
        grabSeq.AddChild(new BTActionWaitBehind());
        grabSeq.AddChild(new BTActionGrab());

        // Fallback branches
        root.AddChild(new BTActionAttack());
        root.AddChild(new BTActionChaseTarget());
    }
}