using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    private readonly Enemy _enemy;

    public EnemyChaseState(Enemy enemy)
    {
        _enemy = enemy;
    }

    // Kept for API compatibility — parameter is intentionally ignored.
    // ROOT CAUSE FIX: the old version cached transitionRange at construction time,
    // which runs in Enemy.Awake() before ApplyData() sets attackRange from the SO.
    // Ranged enemies always got 2f (inspector default) instead of e.g. 12f,
    // causing them to chase until melee distance then immediately retreat.
    // Solution: read _enemy.AttackRange every frame — it's set by ApplyData and stable.
    public EnemyChaseState(Enemy enemy, float _ignored)
    {
        _enemy = enemy;
    }

    public void Enter() =>
        _enemy.GetComponent<EnemyVisionCone>()?.SetAlert(true);

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _enemy.Target.position);

        if (distance <= _enemy.AttackRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.AttackState);
            return;
        }

        if (distance > _enemy.Detection.DetectionRange)
        {
            _enemy.GetComponent<ZoneEnemyTracker>()?.OnPlayerSightLost();
            _enemy.ClearTarget();
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        _enemy.Movement.MoveTowards(_enemy.Target.position);
    }

    public void Exit() =>
        _enemy.GetComponent<EnemyVisionCone>()?.SetAlert(false);
}