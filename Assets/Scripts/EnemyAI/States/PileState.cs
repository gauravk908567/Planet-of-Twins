using UnityEngine;

public class PileState : IEnemyState
{
    private readonly GroupGrabEnemy _enemy;
    private Transform _pileTarget;

    public PileState(GroupGrabEnemy enemy) => _enemy = enemy;

    public void SetPileTarget(Transform target) => _pileTarget = target;

    public void Enter() { }

    public void Update()
    {
        if (_pileTarget == null)
        {
            // Grab ended — go back to chasing original target
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _pileTarget.position);

        if (distance > _enemy.AttackRange)
            _enemy.Movement.MoveTowards(_pileTarget.position);
        else
            _enemy.AttackController.TryAttack();
    }

    public void Exit()
    {
        _pileTarget = null;
    }
}