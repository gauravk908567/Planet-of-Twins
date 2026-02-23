using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private Enemy enemy;

    public EnemyAttackState(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public void Enter() { }

    public void Update()
    {
        if (enemy.Target == null)
        {
            enemy.StateMachine.ChangeState(enemy.IdleState);
            return;
        }

        if (!enemy.Movement.Agent.pathPending && enemy.Movement.Agent.remainingDistance > enemy.AttackRange)
        {
            enemy.StateMachine.ChangeState(enemy.ChaseState);
            return;
        }
        enemy.AttackController.TryAttack();
    }

    public void Exit() { }
}