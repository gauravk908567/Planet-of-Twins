using BehaviourTree;
using HybridGOAP;

/// <summary>
/// GOAP Action: Penitent attack — slow creeping approach + grab trigger.
/// Satisfies GOAPGoalAttackTwin.
/// Grab itself is triggered by PenitentEnemy.Update() proximity check.
/// BT drives the approach with personality.
///
/// Mood-driven cost:
///   Contemptuous — cost 5  (prefers grabbing, feels untouchable)
///   Normal/Annoyed — cost 10 (default)
///   Enraged — cost 15 (too angry to be methodical — planner may prefer charge)
/// </summary>
public class GOAPActionAttackTwinPenitent : GOAPActionBehaviourTree
{
    protected override void PopulateSupportedGoalTypes()
    {
        SupportedGoalTypes = new System.Type[] { typeof(GOAPGoalAttackTwin) };
    }

    protected override ECharacterResources GetRequiredResources()
        => ECharacterResources.Legs | ECharacterResources.Torso;

    public override float CalculateCost()
    {
        // Read mood from Blackboard — set by GOAPBrainPenitentEnemy.OnPreTickBrain
        // via EnemyMoodSystem sync
        int moodInt = (int)EnemyMood.Normal;
        LinkedBlackboard?.TryGet(PoTNames.EnemyMoodState, out moodInt, (int)EnemyMood.Normal);
        var mood = (EnemyMood)moodInt;

        return mood switch
        {
            EnemyMood.Contemptuous => 5f,   // methodical, prefers grab
            EnemyMood.Enraged => 15f,  // too frantic — planner picks charge if available
            _ => 10f,  // Normal, Annoyed, Wounded etc
        };
    }

    protected override void ConfigureBehaviourTree()
    {
        var root = new BTFlowNode_Selector("AttackTwin_Penitent");
        AddChildToRootNode(root);
        root.AddChild(new BTActionPenitentApproach());
    }
}