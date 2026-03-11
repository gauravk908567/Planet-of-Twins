using System.Collections.Generic;
using UnityEngine;

public class ReturnAnimationState : IEnemyState
{
    private readonly Enemy _enemy;
    private readonly float _animDuration;

    private float _timer;
    private List<Enemy> _pausedCombatants = new List<Enemy>();
    private static Enemy[] _allEnemies; // cached scene enemies

    public ReturnAnimationState(Enemy enemy, float animDuration = 1.5f)
    {
        _enemy = enemy;
        _animDuration = animDuration;
    }

    public void Enter()
    {
        _timer = _animDuration;
        _pausedCombatants.Clear();

        // Find all enemies that are currently targeting THIS enemy
        // (they were fighting the possessed enemy and must pause too)
        RefreshEnemyCache();

        foreach (var other in _allEnemies)
        {
            if (other == null || other == _enemy) continue;
            if (other.Target == _enemy.transform)
            {
                other.StateMachine.Pause();
                other.Movement.OnFreeze();
                _pausedCombatants.Add(other);
            }
        }

        // Pause self
        _enemy.StateMachine.Pause();
        _enemy.Movement.OnFreeze();

        // TODO: trigger return animation on Animator here
    }

    public void Update()
    {
        // Timer ticks in real time (StateMachine is paused so this Update()
        // won't actually run — the state machine handles this)
        // Solution: this state owns its own timer via a coroutine started in Enter()
        // See note below — use a coroutine approach instead
    }

    public void Exit()
    {
        // Resume self
        _enemy.StateMachine.Resume();
        _enemy.Movement.OnUnfreeze();

        // Resume direct combatants
        foreach (var other in _pausedCombatants)
        {
            if (other == null) continue;
            other.StateMachine.Resume();
            other.Movement.OnUnfreeze();
        }

        _pausedCombatants.Clear();

        // Restore faction and go idle
        _enemy.OnPossessionEnded();
    }

    private void RefreshEnemyCache()
    {
        _allEnemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
    }
}