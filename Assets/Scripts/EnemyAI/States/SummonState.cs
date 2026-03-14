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
        // FIX: was exiting to ChaseState on frame 1 because _isSummoning was never
        // checked here. The summoner would run away while the spawn coroutine was
        // still mid-execution, cutting off spawns and breaking any summon animation.
        if (_enemy.IsSummoning) return;

        if (_enemy.Target != null)
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
        else
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
    }

    public void Exit() { }
}