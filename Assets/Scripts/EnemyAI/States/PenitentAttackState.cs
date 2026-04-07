using UnityEngine;

/// <summary>
/// Penitent attack state — checks proximity and triggers crush grab.
/// Replaces standard EnemyAttackState for PenitentEnemy.
/// If within crushRange: calls StartCrush() on the grabbed twin.
/// Otherwise: standard melee attack.
/// </summary>
public class PenitentAttackState : IEnemyState
{
    private readonly PenitentEnemy _enemy;
    private readonly PenitentEnemyData _data;

    public PenitentAttackState(PenitentEnemy enemy, PenitentEnemyData data)
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
        float crushRange = _data?.crushRange ?? 1.5f;

        if (dist <= crushRange)
        {
            // Close enough — attempt crush
            var player = _enemy.Target.GetComponent<Player>();
            if (player != null && !player.IsGrabbed)
                _enemy.StartCrush(player);
        }
        else
        {
            // Not close enough — normal melee
            _enemy.AttackController.TryAttack();
        }
    }

    public void Exit() { }
}