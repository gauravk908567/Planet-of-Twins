using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Summon minion while maintaining position.
/// Kites to desired range first, then summons.
/// Falls back to ranged attack if summon not ready.
/// </summary>
public class GOAPActionSummon : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalSummon) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Torso; // legs free — can kite while summoning

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("Summon");
        AddChildToRootNode(root);

        // Branch 1: Summon if in position
        root.AddChild(new BTActionSummon());

        // Branch 2: Kite to position first
        root.AddChild(new BTActionKite());
    }
}