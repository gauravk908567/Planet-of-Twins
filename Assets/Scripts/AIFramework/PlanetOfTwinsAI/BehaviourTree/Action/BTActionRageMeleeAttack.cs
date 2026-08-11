public class BTActionRageMeleeAttack : BTActionAttack
{
    protected override void OnEnter()
    {
        base.OnEnter();
        // Override range to melee range
        var tb = _enemy as TetherBreakerEnemy;
        if (tb != null) _attackRange = tb.MeleeRange;
    }
}