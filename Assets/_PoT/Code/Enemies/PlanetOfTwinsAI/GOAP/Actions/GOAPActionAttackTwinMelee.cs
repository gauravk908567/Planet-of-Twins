using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Melee enemy attack.
/// All values read from EnemyData SO at runtime — no serialized fields.
/// </summary>
public class GOAPActionAttackTwinMelee : GOAPActionBehaviourTree
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
        var root = new BTFlowNode_Selector("AttackTwin_Melee");
        AddChildToRootNode(root);
        root.AddChild(new BTActionAttack());
        root.AddChild(new BTActionChaseTarget());
    }
}