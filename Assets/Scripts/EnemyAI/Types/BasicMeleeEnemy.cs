using UnityEngine;

public class BasicMeleeEnemy : Enemy
{
    // ── VFX cue (EnemyVfxLibrary, R4) ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Melee;
    protected override string MeleeAttackCueId => FxIds.Enemy.Melee.On_MeleeAttack;

    //protected override void InitStates()
    //{
    //    IdleState = new EnemyIdleState(this);
    //    ChaseState = new EnemyChaseState(this);
    //    AttackState = new EnemyAttackState(this);
    //    PossessedState = new PossessedState(this, possessedTargetLayer);
    //}
}