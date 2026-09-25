using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Stay behind target for required duration.
/// If enemy leaves the behind arc, returns Failed immediately
/// so the selector falls back to BTActionGetBehindTarget.
/// Returns Succeeded when timer completes — grab can trigger.
/// </summary>
public class BTActionWaitBehind : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Wait Behind";

    private float _behindTimeRequired;
    private float _behindDotThreshold;
    private float _elapsed;

    protected override void OnEnter()
    {
        base.OnEnter();
        var grabData = _enemy?.Data as GroupGrabEnemyData;
        _behindTimeRequired = grabData?.behindTimeRequired ?? 1.5f;
        _behindDotThreshold = grabData?.behindDotThreshold ?? -0.3f;
        _elapsed = 0f;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Left the behind arc — reset
        if (!IsBehindTarget(target))
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        _elapsed += InDeltaTime;

        if (_elapsed >= _behindTimeRequired)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    private bool IsBehindTarget(Player target)
    {
        Vector3 toEnemy = (_enemy.transform.position - target.transform.position).normalized;
        float dot = Vector3.Dot(target.transform.forward, toEnemy);
        return dot < _behindDotThreshold;
    }
}