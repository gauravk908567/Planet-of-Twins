using UnityEngine;

/// <summary>
/// Tether-Breaker rage state — chain was broken by player mash.
/// Full aggro chase until death. No retreat, no bomb, no chain.
/// </summary>
public class TetherBreakerRageState : IEnemyState
{
    private readonly TetherBreakerEnemy _enemy;
    private readonly TetherBreakerEnemyData _data;

    public TetherBreakerRageState(TetherBreakerEnemy enemy, TetherBreakerEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter()
    {
        float rageSpeed = (_enemy.Data?.moveSpeed ?? 3.5f) *
                          (_data?.rageSpeedMultiplier ?? 1.8f);
        _enemy.Movement.SetSpeed(rageSpeed);
        _enemy.SetRageColour(true);
        Debug.Log("[TetherBreaker] RAGE — full aggro until death");

        // UI + VFX
        var stateUI = _enemy.GetComponentInChildren<EnemyStateUIController>();
        stateUI?.ShowRage(999f); // rage lasts until death — 999f = effectively permanent
        stateUI?.ShowIkariRage();
        _enemy.GetComponentInChildren<EnemyVFXController>()?.PlayRage();
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        // Pure aggro — close distance and melee attack
        // Use a short melee range independent of chainAttackRange
        float dist = Vector3.Distance(_enemy.transform.position, _enemy.Target.position);
        float meleeRange = _data?.attackRange ?? 2f; // base EnemyData attackRange = actual melee

        if (dist <= meleeRange)
            _enemy.AttackController.TryAttack();
        else
            _enemy.Movement.MoveTowards(_enemy.Target.position);
    }

    public void Exit()
    {
        _enemy.SetRageColour(false);
        _enemy.GetComponentInChildren<EnemyStateUIController>()?.HideRage();
        _enemy.GetComponentInChildren<EnemyVFXController>()?.StopRage();
    }
}