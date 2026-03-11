using UnityEngine;

public class FrontAttackState : IEnemyState
{
    private readonly GroupGrabEnemy _enemy;
    private readonly float _behindDotThreshold;

    public FrontAttackState(GroupGrabEnemy enemy, float behindDotThreshold = -0.3f)
    {
        _enemy = enemy;
        _behindDotThreshold = behindDotThreshold;
    }

    public void Enter() { }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _enemy.Target.position);

        if (distance > _enemy.AttackRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        _enemy.AlertNearby(_enemy.Target, isGrab: false);

        // Got behind player — start grab timer
        if (IsEnemyBehindPlayer())
        {
            _enemy.StateMachine.ChangeState(_enemy.BehindTimer);
            return;
        }

        // AttackController.TryAttack() gates its own cooldown — no timer needed here
        _enemy.AttackController?.TryAttack();
    }

    public void Exit() { }

    private bool IsEnemyBehindPlayer()
    {
        Vector3 playerToEnemy = (_enemy.transform.position - _enemy.Target.position).normalized;
        float dot = Vector3.Dot(playerToEnemy, _enemy.Target.forward);
        return dot < _behindDotThreshold;
    }
}