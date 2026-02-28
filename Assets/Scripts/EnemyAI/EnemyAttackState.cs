using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private Enemy enemy;

    public EnemyAttackState(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        enemy.Movement.Agent.isStopped = true;
    }

    public void Update()
    {
        if (enemy.Target == null)
        {
            enemy.StateMachine.ChangeState(enemy.IdleState);
            return;
        }

        float distance = Vector3.Distance(
            enemy.transform.position,
            enemy.Target.position
        );

        if (distance > enemy.AttackRange)
        {
            enemy.StateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        enemy.AttackController.TryAttack();
    }

    public void Exit()
    {
        enemy.Movement.Agent.isStopped = false;
    }
}