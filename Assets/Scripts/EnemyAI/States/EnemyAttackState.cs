using UnityEngine;

public class EnemyAttackState : IEnemyState
{
    private readonly Enemy _enemy;

    public EnemyAttackState(Enemy enemy) => _enemy = enemy;

    public void Enter()
    {
        _enemy.GetComponent<EnemyVisionCone>()?.SetAlert(true);
        _enemy.Movement.Stop();
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

        if (distance > _enemy.AttackRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        _enemy.AttackController.TryAttack();
    }

    public void Exit() { }  // Movement resumes when ChaseState calls MoveTowards
}