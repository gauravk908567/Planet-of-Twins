using BehaviourTree;

/// <summary>
/// BT Action: Ghost pursues soul via direct transform movement.
/// Movement is handled by SiphonGhost.PursuitLoop coroutine.
/// This action holds InProgress to keep GOAP from replanning mid-pursuit.
/// Returns Failed if ghost or soul is null/resolved.
/// </summary>
public class BTActionGhostPursuit : BTActionNodeBase
{
    public override string DebugDisplayName { get; protected set; } = "Ghost Pursuit";

    private SiphonGhost _ghost;

    protected override void OnEnter()
    {
        base.OnEnter();
        var self = LinkedBlackboard?.GetGameObject(CommonCore.Names.Self);
        _ghost = self?.GetComponent<SiphonGhost>();
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_ghost == null || _ghost.IsResolved || _ghost.Soul == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }
}