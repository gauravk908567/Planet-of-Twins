using BehaviourTree;

/// <summary>
/// BT Action: Ghost engages the soul by THROWING its bind-chain.
/// Calls SiphonGhost.TryThrow() once, then holds InProgress while the throw is in flight OR the soul is bound.
/// Returns Succeeded when the engagement ends (chain missed, or bind finished by mash/timer).
/// Retreat/retry after a miss or bind is handled by SiphonGhost coroutines internally.
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
            _ghost.TryThrow();
            _bindStarted = true;
        }

        // Hold while the throw is in flight OR the soul is bound; only then is the engagement resolved.
        if (_ghost.IsThrowing || _ghost.IsBinding)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Engagement ended (missed, or bind finished)
        return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
    }
}