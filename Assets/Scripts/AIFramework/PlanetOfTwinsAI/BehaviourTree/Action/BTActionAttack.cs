using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Melee attack on the detected twin.
/// Reads attackRange from enemy Data SO at runtime.
/// Applies mood attackCooldownMult via EnemyAttackController.SetAttackSlowdown().
/// No constructor parameters needed.
/// </summary>
public class BTActionAttack : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Attack";

    protected float _attackRange;

    protected override void OnEnter()
    {
        base.OnEnter();
        _attackRange = _enemy?.Data?.attackRange ?? 2f;
        ApplyMoodCooldown();
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
        if (dist > _attackRange)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        FaceTarget();
        _enemy.AttackController.TryAttack();

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        // Clear mood cooldown override — restore base cooldown
        _enemy?.AttackController.ClearAttackSlowdown();
    }

    private void ApplyMoodCooldown()
    {
        var ms = _enemy?.GetComponent<EnemyMoodSystem>();
        if (ms == null) return;
        float mult = ms.CurrentModifiers.attackCooldownMult;
        // SetAttackSlowdown expects >= 1 (slowing only) — mood can speed up too
        // so pass directly and let AttackController clamp if needed
        _enemy.AttackController.SetAttackSlowdown(mult);
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