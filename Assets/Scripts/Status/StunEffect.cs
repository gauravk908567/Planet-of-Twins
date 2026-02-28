using UnityEngine;

public class StunEffect : StatusEffectBase
{
    private Enemy enemy;
    private FactionComponent fc;

    public StunEffect(GameObject target, float duration) : base(target, duration)
    {
        enemy = target.GetComponent<Enemy>();
        fc = target.GetComponent<FactionComponent>();
    }

    public override void OnApply()
    {
        base.OnApply();

        if (enemy.Movement != null)
            enemy.Movement.enabled = false;

        if (enemy.StateMachine != null)
            enemy.StateMachine.enabled = false;
        fc.SetDebugText("Stun");
    }

    public override void OnRemove()
    {
        if (enemy.Movement != null)
            enemy.Movement.enabled = true;

        if (enemy.StateMachine != null)
            enemy.StateMachine.enabled = true;
        enemy.StateMachine.ChangeState(enemy.IdleState);
        fc.SetDebugText("Enemy");
    }
}