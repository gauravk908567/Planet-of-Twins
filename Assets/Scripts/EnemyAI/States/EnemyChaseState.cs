using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    private readonly Enemy _enemy;

    // Ranged enemies stop chasing at their launcher's attack range,
    // not the base melee AttackRange. Set via constructor override.
    private readonly float _transitionRange;

    public EnemyChaseState(Enemy enemy)
    {
        _enemy = enemy;
        // Default: use melee attack range from Enemy base
        _transitionRange = enemy.AttackRange;
    }

    /// <summary>
    /// Used by RangedEnemy — transitions to attack at launcher range instead of melee range.
    /// </summary>
    public EnemyChaseState(Enemy enemy, float transitionRange)
    {
        _enemy = enemy;
        _transitionRange = transitionRange;
    }

    public void Enter()
    {
        _enemy.GetComponent<EnemyVisionCone>()?.SetAlert(true);
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

        // In attack range — transition to attack
        if (distance <= _transitionRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.AttackState);
            return;
        }

        // Beyond detection range — lose target
        if (distance > _enemy.Detection.DetectionRange)
        {
            _enemy.GetComponent<ZoneEnemyTracker>()?.OnPlayerSightLost();
            _enemy.ClearTarget();
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        // Still chasing
        _enemy.Movement.MoveTowards(_enemy.Target.position);
    }

    public void Exit()
    {
        _enemy.GetComponent<EnemyVisionCone>()?.SetAlert(false);
    }
}