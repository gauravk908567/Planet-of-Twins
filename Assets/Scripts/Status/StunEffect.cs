using UnityEngine;

public class StunEffect : StatusEffectBase
{
    private EnemyMovement movement;
    private Enemy enemy;

    public StunEffect(GameObject target, float duration): base(target, duration)
    {
        movement = target.GetComponent<EnemyMovement>();
        enemy = target.GetComponent<Enemy>();
    }

    public override void OnApply()
    {
        base.OnApply();

        if (enemy.Movement != null)
            enemy.Movement.enabled = false;

        if (enemy.StateMachine != null)
            enemy.StateMachine.enabled = false;
    }

    public override void OnRemove()
    {
        if (enemy.Movement != null)
            enemy.Movement.enabled = true;

        if (enemy.StateMachine != null)
            enemy.StateMachine.enabled = true;
        enemy.StateMachine.ChangeState(enemy.IdleState);
    }
}