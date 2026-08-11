using UnityEngine;

public class RangedEnemy : Enemy
{
    // firePoint moved to the Enemy base (serialized values on existing prefabs survive — same field name).

    public float MinEngageRange { get; private set; } = 3f;
    public float DesiredRange { get; private set; } = 8f;

    public IEnemyState RetreatState { get; private set; }

    // ── VFX cue (EnemyVfxLibrary, R4) ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Ranged;
    protected override string RangedAttackCueId => FxIds.Enemy.Ranged.On_RangedAttack;

    //protected override void InitStates()
    //{
    //    IdleState = new EnemyIdleState(this);
    //    ChaseState = new EnemyChaseState(this, AttackRange);
    //    AttackState = new RangedAttackState(this);
    //    RetreatState = new RetreatState(this);
    //    PossessedState = new PossessedState(this, possessedTargetLayer);
    //}

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);  // sets attackRange, damage, cooldown, windup, speed, health

        if (data is RangedEnemyData rangedData)
        {
            MinEngageRange = rangedData.minEngageRange;
            DesiredRange = rangedData.desiredRange;

            // Mark the kiting archetype. The projectile itself is wired by base.ApplyData above
            // (SetProjectile from EnemyData) — nothing type-specific left here. States read enemy
            // properties at runtime (DesiredRange, AttackRange, MinEngageRange), so no recreation.
            AttackController.SetRangedMode();
        }
        else
        {
            Debug.LogWarning($"[RangedEnemy] {name} — expected RangedEnemyData, got " +
                $"{data?.GetType().Name}. Ranged stats not applied.", this);
        }
    }
}