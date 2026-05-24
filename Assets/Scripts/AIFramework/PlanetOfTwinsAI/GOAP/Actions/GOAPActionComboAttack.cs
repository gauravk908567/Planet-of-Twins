using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Execute combo attack or rally to RallyPoint.
/// Satisfies GOAPGoalComboAttack.
///
/// BT logic:
///   Selector:
///     BTActionComboRally   ← if rallying, move to RallyPoint first
///     BTActionComboAttack  ← if committing/third wheel, execute attack
/// </summary>
public class GOAPActionComboAttack : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalComboAttack) };
    }

    protected override ECharacterResources GetRequiredResources()
    => ECharacterResources.Legs;

    public override float CalculateCost() => 3f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("ComboAttack");
        AddChildToRootNode(root);
        root.AddChild(new BTActionComboAttack());
    }
}