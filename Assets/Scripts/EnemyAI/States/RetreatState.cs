using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// RetreatState
// Backs away from the target until DesiredRange is reached, then returns
// to RangedAttackState. Reads ranges from RangedEnemy properties at runtime
// so ApplyData() changes take effect without reconstructing the state.
// ─────────────────────────────────────────────────────────────────────────────
public class RetreatState : IEnemyState
{
    private readonly RangedEnemy _enemy;

    public RetreatState(RangedEnemy enemy)
    {
        _enemy = enemy;
    }

    public void Enter() { }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _enemy.Target.position);

        // Backed off far enough — return to shooting
        if (distance >= _enemy.DesiredRange)
        {
            _enemy.StateMachine.ChangeState(_enemy.AttackState);
            return;
        }

        // Move directly away from target
        Vector3 awayDir = (_enemy.transform.position - _enemy.Target.position).normalized;
        Vector3 retreatPos = _enemy.transform.position + awayDir * 2f;
        _enemy.Movement.MoveTowards(retreatPos);
    }

    public void Exit() { }
}