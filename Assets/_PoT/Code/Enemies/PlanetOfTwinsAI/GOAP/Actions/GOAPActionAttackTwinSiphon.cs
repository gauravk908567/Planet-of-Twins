using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Siphon attack behaviour.
/// BT Structure:
///   Selector (root)
///   ├── Sequence "Panic" — twin in panic range → bomb then flee
///   │     BTActionSpawnPanicBomb
///   │     BTActionKite
///   └── Sequence "KiteAndShoot" — maintain range and shoot
///         BTActionRangedAttack  — shoot if in attack range, fails if out of range
///         BTActionKite          — position to desired range
///
/// KiteAndShoot selector tries ranged attack first.
/// If out of attack range, ranged attack fails → kite closes/holds distance.
/// Once kite returns Succeeded (at desired range), next tick tries attack again.
/// </summary>
public class GOAPActionAttackTwinSiphon : GOAPActionBehaviourTree
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
        var root = new BTFlowNode_Selector("AttackTwin_Siphon");
        AddChildToRootNode(root);

        // Branch 1: Panic — bomb + flee when twin too close
        var panicSeq = new BTFlowNode_Sequence("Panic");
        root.AddChild(panicSeq);
        panicSeq.AddChild(new BTActionSpawnPanicBomb());
        panicSeq.AddChild(new BTActionKite());

        // Branch 2: Kite and shoot
        var kiteShootSeq = new BTFlowNode_Sequence("KiteAndShoot");
        root.AddChild(kiteShootSeq);
        kiteShootSeq.AddChild(new BTActionKite());
        kiteShootSeq.AddChild(new BTActionRangedAttack());
    }
}