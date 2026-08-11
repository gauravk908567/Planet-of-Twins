using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Ranged attack — fires arrow at target.
/// Reads attackRange and minEngageRange from enemy Data SO at runtime.
/// Returns Failed when target is closer than minEngageRange
/// so the selector falls through to BTActionKite.
/// No constructor parameters needed.
/// </summary>
public class BTActionRangedAttack : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Ranged Attack";

    private float _attackRange;
    private float _minEngageRange;

    protected override void OnEnter()
    {
        base.OnEnter();

        var rangedData = _enemy?.Data as RangedEnemyData;
        _attackRange = _enemy?.Data?.attackRange ?? 10f;
        _minEngageRange = rangedData?.minEngageRange ?? 4f;

        FaceTarget();
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = Vector3.Distance(_enemy.transform.position, target.transform.position);

        // Too close — fail so selector falls through to BTActionKite
        if (dist < _minEngageRange)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Too far — fail so selector falls through to BTActionKite (close-in branch)
        if (dist > _attackRange)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        FaceTarget();
        _enemy.AttackController.TryRangedAttack(target.transform);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    private void FaceTarget()
    {
        var target = GetBestTarget();
        if (target == null || _enemy == null) return;
        Vector3 dir = (target.transform.position - _enemy.transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);
    }
}