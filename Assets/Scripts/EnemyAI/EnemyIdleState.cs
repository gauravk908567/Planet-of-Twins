using UnityEngine;

public class EnemyIdleState : IEnemyState
{
    private Enemy enemy;
    private float wanderTimer;
    private Vector3 wanderTarget;

    public EnemyIdleState(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        PickNewPoint();
    }

    public void Update()
    {
        Transform target = enemy.Detection.DetectTarget(
            enemy.Faction.CurrentFaction
        );

        if (target != null)
        {
            enemy.Target = target;
            enemy.StateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        wanderTimer -= Time.deltaTime;

        enemy.Movement.MoveTowards(wanderTarget);

        if (wanderTimer <= 0f)
        {
            PickNewPoint();
        }
    }

    public void Exit() { }

    private void PickNewPoint()
    {
        wanderTimer = Random.Range(2f, 4f);

        Vector3 randomOffset =
            new Vector3(Random.Range(-3, 3), 0, Random.Range(-3, 3));

        wanderTarget = enemy.transform.position + randomOffset;
    }
}