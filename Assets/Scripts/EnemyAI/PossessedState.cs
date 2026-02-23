using UnityEngine;

public class PossessedState : IEnemyState
{
    private Enemy enemy;

    public PossessedState(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public void Enter() { }

    public void Update()
    {
        Transform target = enemy.Detection.DetectTarget(
            enemy.Faction.CurrentFaction
        );

        if (target == null)
            return;

        float distance = Vector3.Distance(
            enemy.transform.position,
            target.position
        );

        if (distance > enemy.AttackRange)
        {
            enemy.Movement.MoveTowards(target.position);
        }
        else
        {
            enemy.AttackController.TryAttack();
        }
    }

    public void Exit() { }
}