using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Join a pile — move to encircle a grabbed player.
/// Reads EngagedTarget from shared Blackboard.
/// Positions enemy around the grabbed player at a random offset.
///
/// REUSABLE — any enemy type can use this action.
/// Add to BT under a condition that checks TargetIsEngaged.
/// </summary>
public class BTActionJoinPile : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Join Pile";

    private Vector3 _pileOffset;
    private float _arrivalRange = 1.5f;

    protected override void OnEnter()
    {
        base.OnEnter();

        // Pick a random offset around the grabbed player
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        _pileOffset = new Vector3(
            Mathf.Cos(angle) * _arrivalRange,
            0f,
            Mathf.Sin(angle) * _arrivalRange);

        _enemy?.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        Debug.Log($"[BTActionJoinPile] running on {_enemy?.name}");
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Read grabbed target from shared Blackboard
        var shared = CommonCore.BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        if (shared == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        bool engaged = false;
        shared.TryGet(PoTNames.TargetIsEngaged, out engaged, false);
        if (!engaged)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        GameObject target = null;
        shared.TryGet(PoTNames.EngagedTarget, out target, null);
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        Vector3 destination = target.transform.position + _pileOffset;
        bool arrived = MoveToward(destination, 0.5f);

        if (arrived)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress); // hold position

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
    }
}