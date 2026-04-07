using UnityEngine;

/// <summary>
/// Witness shadow state — follows its summoned melee ally.
/// Transitions to WitnessRitualState when ally dies.
/// Throws bomb if twin enters bombTriggerRange.
/// </summary>
public class WitnessShadowState : IEnemyState
{
    private readonly WitnessEnemy _enemy;
    private readonly WitnessEnemyData _data;

    public WitnessShadowState(WitnessEnemy enemy, WitnessEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter()
    {
        _enemy.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
    }

    public void Update()
    {
        // Ally died — start ritual to summon a new one
        if (_enemy.FollowTarget == null || _enemy.FollowTarget.Health.IsDead)
        {
            _enemy.StateMachine.ChangeState(_enemy.RitualState);
            return;
        }

        // Shadow the ally — stay close but behind it
        float distToFollow = Vector3.Distance(
            _enemy.transform.position, _enemy.FollowTarget.transform.position);

        if (distToFollow > 2.5f) // shadowFollowDistance — tune in WitnessEnemy.ShadowFollowDist
            _enemy.Movement.MoveTowards(_enemy.FollowTarget.transform.position);
        else
            _enemy.Movement.Stop();

        // Face nearest twin
        var twin = _enemy.GetNearestTwin();
        if (twin != null)
        {
            Vector3 dir = (twin.transform.position - _enemy.transform.position);
            dir.y = 0f;
            if (dir != Vector3.zero)
                _enemy.transform.rotation = Quaternion.LookRotation(dir);
        }

        // Bomb throw if twin too close
        _enemy.CheckBombThrow();
    }

    public void Exit()
    {
        _enemy.Movement.Stop();
    }

}