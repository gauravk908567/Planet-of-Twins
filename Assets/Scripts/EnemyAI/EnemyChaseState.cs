using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    private Enemy enemy;

    public EnemyChaseState(Enemy enemy)
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

        enemy.Movement.MoveTowards(enemy.Target.position);

        if (enemy.Detection.IsPlayerInRange(enemy.AttackRange, enemy.Target))
        {
            enemy.StateMachine.ChangeState(enemy.AttackState);
        }
    }

    public void Exit() { }
}