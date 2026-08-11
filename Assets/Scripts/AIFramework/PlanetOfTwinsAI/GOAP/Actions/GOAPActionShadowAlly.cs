using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Shadow ally + bomb if twin enters range.
/// Satisfies GOAPGoalShadowAlly.
///
/// BT:
///   Selector
///   ├── Sequence "Bomb" — twin in range → throw bomb → retreat
///   │     BTActionThrowBomb
///   │     BTActionWitnessRetreat
///   └── BTActionShadowAlly — follow ally
/// </summary>
public class GOAPActionShadowAlly : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalShadowAlly) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("ShadowAlly");
        AddChildToRootNode(root);

        // Branch 1: Bomb + retreat if twin in range
        var bombSeq = new BTFlowNode_Sequence("BombAndRetreat");
        root.AddChild(bombSeq);
        bombSeq.AddChild(new BTActionThrowBomb());
        bombSeq.AddChild(new BTActionWitnessRetreat());

        // Branch 2: Shadow ally
        root.AddChild(new BTActionShadowAlly());
    }
}