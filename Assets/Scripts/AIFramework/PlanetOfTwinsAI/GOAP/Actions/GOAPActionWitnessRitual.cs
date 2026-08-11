using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Ritual summon — uses smart ritual path if sites available,
/// falls back to original retreat-and-ritual behaviour.
///
/// BT:
///   Selector
///   ├── BTActionWitnessRitualPath  — sprint to safe site, drop bomb, ritual there
///   ├── Sequence "Ritual"          — fallback: channel at current position
///   │     BTActionWitnessRitual
///   └── BTActionWitnessRetreat     — flee when interrupted
/// </summary>
public class GOAPActionWitnessRitual : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalWitnessRitual) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost() => 10f;

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("WitnessRitual");
        AddChildToRootNode(root);

        // Branch 1: Smart path to ritual site (fails if no sites in zone)
        root.AddChild(new BTActionWitnessRitualPath());

        // Branch 2: Original — channel ritual at current position
        var ritualSeq = new BTFlowNode_Sequence("Ritual");
        root.AddChild(ritualSeq);
        ritualSeq.AddChild(new BTActionWitnessRitual());

        // Branch 3: Flee if interrupted
        root.AddChild(new BTActionWitnessRetreat());
    }
}