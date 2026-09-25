using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Rage chase — sprint at target at rage speed.
/// Reads rageSpeedMultiplier from TetherBreakerEnemyData.
/// Reusable for any enemy with a rage speed boost.
/// Never returns Succeeded — rage lasts until death.
/// </summary>
public class BTActionRageChase : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Rage Chase";

    protected override void OnEnter()
    {
        base.OnEnter();
        if (_enemy == null) return;

        float rageMultiplier = 1.8f;
        if (_enemy.Data is TetherBreakerEnemyData tbData)
            rageMultiplier = tbData.rageSpeedMultiplier;

        _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed * rageMultiplier);
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        _enemy.Movement.MoveTowards(target.transform.position);

        // Face target
        Vector3 dir = (target.transform.position - _enemy.transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_enemy != null)
            _enemy.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
    }
}