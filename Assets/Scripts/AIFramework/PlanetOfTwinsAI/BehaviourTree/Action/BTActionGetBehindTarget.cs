using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Circle to get behind the target.
/// Moves to a position behind the target based on dot product threshold.
/// Returns Succeeded when enemy is behind player.
/// Returns Failed if no target.
///
/// Reusable — any enemy that needs to flank can use this.
/// </summary>
public class BTActionGetBehindTarget : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Get Behind Target";

    private float _behindDotThreshold;

    protected override void OnEnter()
    {
        base.OnEnter();
        var grabData = _enemy?.Data as GroupGrabEnemyData;
        _behindDotThreshold = grabData?.behindDotThreshold ?? -0.3f;
        _enemy?.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Check if already behind
        if (IsBehindTarget(target))
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        // Circle around to get behind — move to position behind target
        Vector3 behindPos = target.transform.position - target.transform.forward * 1.5f;
        behindPos += new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
        _enemy.Movement.MoveTowards(behindPos);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
    }

    private bool IsBehindTarget(Player target)
    {
        Vector3 toEnemy = (_enemy.transform.position - target.transform.position).normalized;
        float dot = Vector3.Dot(target.transform.forward, toEnemy);
        return dot < _behindDotThreshold;
    }
}