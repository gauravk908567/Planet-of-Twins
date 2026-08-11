using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Face twin, throw bomb, retreat.
/// Reusable — reads bomb data from WitnessEnemyData.
/// Returns Succeeded when bomb thrown.
/// Returns Failed if bomb not ready or no target.
/// </summary>
public class BTActionThrowBomb : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "ThrowBomb";

    private WitnessEnemy _witness;

    protected override void OnEnter()
    {
        base.OnEnter();
        _witness = _enemy as WitnessEnemy;
        _enemy?.Movement.Stop();
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_witness == null || !_witness.CanThrowBomb)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Already throwing — hold
        if (_witness.IsThrowing)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Throw just finished
        if (LastStatus == EBTNodeResult.InProgress && !_witness.IsThrowing)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Face target then throw
        Vector3 dir = (target.transform.position - _enemy.transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);

        _witness.ThrowBomb(target.transform);
        Debug.Log($"[ThrowBomb] {_enemy.name} throwing bomb at {target.name}");

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_enemy?.Data != null)
            _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed);
    }
}