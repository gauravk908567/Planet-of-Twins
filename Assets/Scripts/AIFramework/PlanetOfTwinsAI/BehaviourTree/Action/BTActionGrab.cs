using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Execute the grab.
/// Calls GroupGrabEnemy.StartGrab() and stays InProgress while grabbing.
/// Returns Failed if grab couldn't start (cooldown, target already grabbed).
/// Returns Succeeded when grab ends (rescue or kill resolved externally).
///
/// The TTK countdown runs in GroupGrabEnemy.Update() — this action
/// just holds InProgress while _isGrabbing is true.
/// </summary>
public class BTActionGrab : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Grab";

    private GroupGrabEnemy _grabEnemy;

    protected override void OnEnter()
    {
        base.OnEnter();
        _grabEnemy = _enemy as GroupGrabEnemy;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        var target = GetBestTarget();
        if (target != null) _grabEnemy.SetTarget(target.transform);

        bool started = _grabEnemy.StartGrab();
        if (_grabEnemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Already grabbing — hold InProgress
        if (_grabEnemy.IsGrabbing)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Grab resolved externally (rescue/kill) — succeeded
        if (LastStatus == EBTNodeResult.InProgress)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        // Try to start grab
        /*bool*/started = _grabEnemy.StartGrab();
        if (!started)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        // If BT exits while still grabbing (e.g. enemy stunned), release
        if (_grabEnemy != null && _grabEnemy.IsGrabbing)
            _grabEnemy.EndGrab();
    }
}