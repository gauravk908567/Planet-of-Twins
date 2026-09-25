using UnityEngine;

public class RangedEnemy : Enemy
{
    // firePoint moved to the Enemy base (serialized values on existing prefabs survive — same field name).

    // ── VFX cue (EnemyVfxLibrary, R4) ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Ranged;
    protected override string RangedAttackCueId => FxIds.Enemy.Ranged.On_RangedAttack;

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);  // sets attackRange, damage, cooldown, windup, speed, health

        if (data is RangedEnemyData)
        {
            // Kiting archetype — BTActionKite reads minEngageRange/desiredRange from the SO
            // at runtime; the projectile is wired by base.ApplyData (SetProjectile from EnemyData).
            AttackController.SetRangedMode();
        }
        else
        {
            Debug.LogWarning($"[RangedEnemy] {name} — expected RangedEnemyData, got " +
                $"{data?.GetType().Name}. Ranged stats not applied.", this);
        }
    }
}
