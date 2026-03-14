using UnityEngine;

/// <summary>
/// Combined attack state for SummonerEnemy.
/// Priorities each frame:
///   1. Too close → retreat
///   2. Too far   → chase
///   3. Summon ready + minion count under cap → summon
///   4. Otherwise → ranged attack (keeps pressure while summon on cooldown)
/// </summary>
public class SummonerAttackState : IEnemyState
{
    private readonly SummonerEnemy _enemy;
    private EnemyVisionCone _visionCone;

    public SummonerAttackState(SummonerEnemy enemy)
    {
        _enemy = enemy;
    }

    public void Enter()
    {
        _enemy.Movement.Stop();
        _visionCone = _enemy.GetComponent<EnemyVisionCone>();
        _visionCone?.SetAlert(true);
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _enemy.Target.position);

        // 1. Too close — back off
        if (distance < _enemy.MinEngageRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.RetreatState);
            return;
        }

        // 2. Too far — close gap
        if (distance > _enemy.AttackRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        // Face target
        Vector3 dir = (_enemy.Target.position - _enemy.transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);

        // 3. Summon if ready and under minion cap
        if (_enemy.CanSummon)
        {
            _enemy.TriggerSummon();
            return;
        }

        // 4. Regular ranged attack while summon recharges
        bool hasLoS = _visionCone == null || _visionCone.IsTargetVisible(_enemy.Target);
        if (!hasLoS) return;

        _enemy.AttackController.TryRangedAttack(_enemy.Target);
    }

    public void Exit()
    {
        _visionCone?.SetAlert(false);
    }
}