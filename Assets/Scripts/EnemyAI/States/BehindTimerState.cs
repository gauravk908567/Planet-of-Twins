using UnityEngine;

public class BehindTimerState : IEnemyState
{
    private readonly GroupGrabEnemy _enemy;
    private readonly float _requiredBehindTime; // default 1.5s
    private readonly float _behindDotThreshold; // default -0.3 (rear 120° arc)

    private float _alertTimer = 0.5f;

    private float _behindTimer = 0f;

    public BehindTimerState(GroupGrabEnemy enemy,
        float requiredBehindTime = 1.5f,
        float behindDotThreshold = -0.3f)
    {
        _enemy = enemy;
        _requiredBehindTime = requiredBehindTime;
        _behindDotThreshold = behindDotThreshold;
    }

    public void Enter()
    {
        _behindTimer = 0f;
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        _alertTimer -= Time.deltaTime;
        if (_alertTimer <= 0f)
        {
            _enemy.AlertNearby(_enemy.Target, isGrab: false);
            _alertTimer = 0.5f;
        }

        // ADD: if target player is already grabbed — don't try to grab, attack instead
        var targetPlayer = _enemy.Target.GetComponent<Player>();
        if (targetPlayer != null && targetPlayer.IsGrabbed)
        {
            _enemy.StateMachine.ChangeState(_enemy.FrontAttackState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _enemy.Target.position);

        if (distance > _enemy.AttackRange)
        {
            _behindTimer = 0f;
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        if (IsEnemyBehindPlayer())
        {
            _behindTimer += Time.deltaTime;

            // ADD: check if any other enemy is nearby — solo grabber attacks instead

            _enemy.AlertNearby(_enemy.Target, isGrab: false);

            bool hasNearbyAlly = HasNearbyAlly();
            if (!hasNearbyAlly)
            {
                // Solo — reset timer, fall through to attack
                _behindTimer = 0f;
                _enemy.StateMachine.ChangeState(_enemy.FrontAttackState);
                return;
            }

            if (_behindTimer >= _requiredBehindTime)
                _enemy.StateMachine.ChangeState(_enemy.GrabState);
        }
        else
        {
            _behindTimer = 0f;
            _enemy.StateMachine.ChangeState(_enemy.FrontAttackState);
        }
    }

    // ADD: check if at least one other enemy is within alert range
    private bool HasNearbyAlly()
    {
        var buffer = new Collider[4];
        int count = Physics.OverlapSphereNonAlloc(
            _enemy.transform.position, _enemy.ChaseAlertRange, buffer,
            _enemy.EnemyLayer);

        for (int i = 0; i < count; i++)
        {
            var other = buffer[i].GetComponent<Enemy>();
            if (other != null && other != _enemy) return true;
        }
        return false;
    }

    public void Exit()
    {
        _behindTimer = 0f;
    }

    private bool IsEnemyBehindPlayer()
    {
        if (_enemy.Target == null) return false;

        // Direction from player to enemy (is enemy behind the player?)
        Vector3 playerToEnemy = (_enemy.transform.position - _enemy.Target.position).normalized;
        Vector3 playerForward = _enemy.Target.forward;

        // Dot < threshold means enemy is in player's rear arc
        float dot = Vector3.Dot(playerToEnemy, playerForward);
        return dot < _behindDotThreshold;
    }
}
