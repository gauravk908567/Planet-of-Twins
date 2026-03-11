using UnityEngine;

public class EnemyIdleState : IEnemyState
{
    private readonly Enemy _enemy;
    private float _wanderTimer;
    private Vector3 _wanderTarget;

    public EnemyIdleState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public void Enter()
    {
        _enemy.GetComponent<EnemyVisionCone>()?.SetAlert(false);

        PickNewPoint();
    }

    public void Update()
    {
        Transform target = _enemy.Detection.DetectTarget(_enemy.FactionComp.CurrentFaction);

        if (target != null)
        {
            _enemy.SetTarget(target);   // FIX: use SetTarget() not Target =
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        _wanderTimer -= Time.deltaTime;
        _enemy.Movement.MoveTowards(_wanderTarget);

        if (_wanderTimer <= 0f)
            PickNewPoint();
    }

    public void Exit() { }

    private void PickNewPoint()
    {
        float radius = _enemy.Data?.wanderRadius ?? 3f;
        float tMin = _enemy.Data?.wanderTimerMin ?? 2f;
        float tMax = _enemy.Data?.wanderTimerMax ?? 4f;

        _wanderTimer = Random.Range(tMin, tMax);
        Vector3 offset = new Vector3(
            Random.Range(-radius, radius), 0,
            Random.Range(-radius, radius));
        _wanderTarget = _enemy.transform.position + offset;
    }
}