using UnityEngine;

public class RangedEnemy : Enemy
{
    [Header("Ranged — Fire Point")]
    [Tooltip("Assign the muzzle/bow tip child transform here on the prefab.")]
    [SerializeField] private Transform firePoint;

    public float MinEngageRange { get; private set; } = 3f;
    public float DesiredRange { get; private set; } = 8f;

    public IEnemyState RetreatState { get; private set; }

    protected override void InitStates()
    {
        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this, AttackRange);
        AttackState = new RangedAttackState(this);
        RetreatState = new RetreatState(this);
        PossessedState = new PossessedState(this, possessedTargetLayer);
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);

        if (data is RangedEnemyData rangedData)
        {
            MinEngageRange = rangedData.minEngageRange;
            DesiredRange = rangedData.desiredRange;

            // firePoint lives on the prefab — SO only carries non-scene data
            AttackController.SetRangedMode(
                rangedData.useProjectile,
                rangedData.projectilePrefab,
                firePoint,                 // from prefab Inspector slot
                rangedData.projectileSpeed);

            ChaseState = new EnemyChaseState(this, data.attackRange);
            AttackState = new RangedAttackState(this);
            RetreatState = new RetreatState(this);
        }
        else
        {
            Debug.LogWarning($"[RangedEnemy] {name} — expected RangedEnemyData, got " +
                $"{data?.GetType().Name}. Ranged stats not applied.", this);
        }
    }
}