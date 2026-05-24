using UnityEngine;

public class BossEnemy : Enemy
{
    // Boss behaviour designed separately once phases are defined.
    // Inherits full Enemy system: health, faction, time-affected,
    // state machine, detection, attack controller.
    //protected override void InitStates()
    //{
    //    IdleState = new EnemyIdleState(this);
    //    ChaseState = new EnemyChaseState(this);
    //    AttackState = new EnemyAttackState(this);

    //    PossessedState = new PossessedState(this, possessedTargetLayer);

    //}

    protected override void HandleDeath()
    {
        // TODO: trigger boss death cutscene / level complete
        base.HandleDeath();
    }
}