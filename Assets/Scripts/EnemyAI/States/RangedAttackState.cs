using UnityEngine;

public class RangedAttackState : IEnemyState
{
    private readonly RangedEnemy _enemy;
    private EnemyVisionCone _visionCone;

    public RangedAttackState(RangedEnemy enemy)
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

        if (distance < _enemy.MinEngageRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.RetreatState);
            return;
        }

        if (distance > _enemy.AttackRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        // Face target
        Vector3 dir = (_enemy.Target.position - _enemy.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);

        bool hasLoS = _visionCone == null || _visionCone.IsTargetVisible(_enemy.Target);
        if (!hasLoS) return;

        // AttackController owns all damage — same as melee, different path
        _enemy.AttackController.TryRangedAttack(_enemy.Target);
    }

    public void Exit()
    {
        _visionCone?.SetAlert(false);
    }
}