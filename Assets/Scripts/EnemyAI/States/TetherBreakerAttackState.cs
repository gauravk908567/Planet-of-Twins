using UnityEngine;

/// <summary>
/// Tether-Breaker attack state — locks position and fires chain.
/// Replaces standard EnemyAttackState.
/// Panic bomb check handled in TetherBreakerEnemy.Update().
/// </summary>
public class TetherBreakerAttackState : IEnemyState
{
    private readonly TetherBreakerEnemy _enemy;
    private readonly TetherBreakerEnemyData _data;

    public TetherBreakerAttackState(TetherBreakerEnemy enemy, TetherBreakerEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter()
    {
        _enemy.Movement.Stop();
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float dist = Vector3.Distance(_enemy.transform.position, _enemy.Target.position);
        float chainRange = _data?.chainAttackRange ?? 8f;

        // Drifted out of chain range — chase again
        if (dist > chainRange * 1.2f)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        // Try chain attack — uses chainAttackRange, independent of AttackRange
        _enemy.TryChainAttack();
    }

    public void Exit() { }
}