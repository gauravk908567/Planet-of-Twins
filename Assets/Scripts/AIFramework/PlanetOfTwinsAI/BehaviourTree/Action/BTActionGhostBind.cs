using BehaviourTree;

/// <summary>
/// BT Action: Ghost binds soul.
/// Calls SiphonGhost.TryBind() once then holds InProgress while binding.
/// Returns Succeeded when bind ends (mash success or timer expire).
/// Retreat after bind is handled by SiphonGhost coroutine internally.
/// Returns Failed if ghost is null or resolved.
/// </summary>
public class BTActionGhostBind : BTActionNodeBase
{
    public override string DebugDisplayName { get; protected set; } = "Ghost Bind";

    private SiphonGhost _ghost;
    private bool _bindStarted;

    protected override void OnEnter()
    {
        base.OnEnter();
        var self = LinkedBlackboard?.GetGameObject(CommonCore.Names.Self);
        _ghost = self?.GetComponent<SiphonGhost>();
        _bindStarted = false;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_ghost == null || _ghost.IsResolved)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (!_bindStarted)
        {
            _ghost.TryBind();
            _bindStarted = true;
        }

        if (_ghost.IsBinding)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Bind ended
        return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
    }
}