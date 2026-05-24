using UnityEngine;

public class RangedEnemy : Enemy
{
    [Header("Ranged — Fire Point")]
    [Tooltip("Assign the muzzle/bow tip child transform here on the prefab.")]
    [SerializeField] private Transform firePoint;

    public float MinEngageRange { get; private set; } = 3f;
    public float DesiredRange { get; private set; } = 8f;

    public IEnemyState RetreatState { get; private set; }

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

            // Wire the attack controller's ranged mode — this is the only thing
            // that needs to happen here. The states themselves read from enemy
            // properties at runtime (DesiredRange, AttackRange, MinEngageRange)
            // so there is no need to recreate them. Recreating them was causing
            // stale state references if ApplyData was called while the state
            // machine was already running.
            AttackController.SetRangedMode(
                rangedData.useProjectile,
                rangedData.projectilePrefab,
                firePoint,
                rangedData.projectileSpeed);

           // Debug.Log($"[RangedEnemy] SetRangedMode called — useProjectile={rangedData.useProjectile} prefab={rangedData.projectilePrefab?.name} firePoint={firePoint?.name}");
        }
        else
        {
            Debug.LogWarning($"[RangedEnemy] {name} — expected RangedEnemyData, got " +
                $"{data?.GetType().Name}. Ranged stats not applied.", this);
        }
    }
}