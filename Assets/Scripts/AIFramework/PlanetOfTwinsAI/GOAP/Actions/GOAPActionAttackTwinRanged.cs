using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Ranged enemy attack.
/// All values read from RangedEnemyData SO at runtime — no serialized fields.
/// </summary>
public class GOAPActionAttackTwinRanged : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalAttackTwin) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("AttackTwin_Ranged");
        AddChildToRootNode(root);
        root.AddChild(new BTActionRangedAttack());
        root.AddChild(new BTActionKite());
    }
}