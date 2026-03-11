using UnityEngine;

public class SummonState : IEnemyState
{
    private readonly SummonerEnemy _enemy;

    public SummonState(SummonerEnemy enemy) => _enemy = enemy;

    public void Enter()
    {
        _enemy.Movement.Stop();
        _enemy.TriggerSummon();
    }

    public void Update()
    {
        // Return to chase after summon cooldown (handled in SummonerEnemy)
        if (_enemy.Target != null)
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
        else
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
    }

    public void Exit() { }
}
