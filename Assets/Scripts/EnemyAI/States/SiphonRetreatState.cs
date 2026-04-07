using UnityEngine;

/// <summary>
/// Siphon retreat � player came too close (panic range).
/// Drops bomb at current position then flees at boosted speed.
/// Returns to KiteState once safe distance is re-established.
/// </summary>
public class SiphonRetreatState : IEnemyState
{
    private readonly SiphonEnemy _enemy;
    private readonly SiphonEnemyData _data;
    private bool _bombDropped;

    public SiphonRetreatState(SiphonEnemy enemy, SiphonEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter()
    {
        _bombDropped = false;
        _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed * _data.retreatSpeedMultiplier);
        _enemy.GetComponentInChildren<EnemyVFXController>()?.PlayPanic();
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        // Drop bomb once on entry
        if (!_bombDropped)
        {
            _bombDropped = true;
            _enemy.SpawnPanicBomb();
        }

        // Flee directly away from target
        Vector3 fleeDir = (_enemy.transform.position - _enemy.Target.position).normalized;
        fleeDir.y = 0f;
        _enemy.Movement.MoveTowards(_enemy.transform.position + fleeDir * 5f);

        // Check if safe distance re-established
        float dist = Vector3.Distance(_enemy.transform.position, _enemy.Target.position);
        if (dist >= _data.preferredRange)
            _enemy.StateMachine.ChangeState(_enemy.KiteState);
    }

    public void Exit()
    {
        _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed);
        _enemy.GetComponentInChildren<EnemyVFXController>()?.StopPanic();
    }
}