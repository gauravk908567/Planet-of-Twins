using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Possessed enemy attacks nearest non-possessed ally.
/// Satisfies GOAPGoalPossessed.
/// Prioritises enemies actively targeting a twin — maximum disruption.
/// BT: single BTActionAttackEnemy node handles scan, chase, and attack.
/// </summary>
public class GOAPActionPossessed : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalPossessed) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 1f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("Possessed");
        AddChildToRootNode(root);
        var action = new BTActionAttackEnemy();
        action.SetPossessionContext(true);
        root.AddChild(action);
    }
}