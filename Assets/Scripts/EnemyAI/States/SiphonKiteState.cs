using UnityEngine;

/// <summary>
/// Siphon's primary combat state — maintain preferred range, attack when in range.
/// Transitions to SiphonRetreatState if player closes within panicRange.
/// </summary>
public class SiphonKiteState : IEnemyState
{
    private readonly SiphonEnemy _enemy;
    private readonly SiphonEnemyData _data;

    public SiphonKiteState(SiphonEnemy enemy, SiphonEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter() { }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float dist = Vector3.Distance(_enemy.transform.position, _enemy.Target.position);

        // Too close — panic retreat
        if (dist < _data.panicRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.RetreatState);
            return;
        }

        // Within attack range — attack
        if (dist <= _enemy.AttackRange)
        {
            _enemy.AttackController.TryAttack();
            // Hold position while attacking
            _enemy.Movement.Stop();
            return;
        }

        // Too far — close distance slowly while staying at preferred range
        if (dist > _data.preferredRange)
            _enemy.Movement.MoveTowards(_enemy.Target.position);
        else
            _enemy.Movement.Stop(); // at preferred range — hold and wait to attack
    }

    public void Exit() { }
}