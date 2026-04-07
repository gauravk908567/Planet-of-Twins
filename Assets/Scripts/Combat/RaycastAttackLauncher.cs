using UnityEngine;

public class RaycastAttackLauncher : MonoBehaviour, IProjectileLauncher
{
    [Header("Stats — set via RangedEnemyData through SetStats()")]
    [SerializeField] private float attackRange = 20f;
    [SerializeField] private float cooldown = 3f;
    [SerializeField] private float damage = 15f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private Transform firePoint;

    private float _lastFireTime = -99f;
    private Enemy _owner;

    public float AttackRange => attackRange;
    public float Cooldown => cooldown;

    private void Awake()
    {
        _owner = GetComponent<Enemy>();
    }

    public bool TryFire(Transform target)
    {
        if (target == null) return false;
        if (Time.time < _lastFireTime + cooldown) return false;

        _lastFireTime = Time.time;

        Transform origin = firePoint != null ? firePoint : transform;
        Vector3 dir = (target.position - origin.position).normalized;

        if (Physics.Raycast(origin.position, dir, out RaycastHit hit, attackRange, hitLayers))
        {
            var playerHealth = hit.collider.GetComponent<PlayerHealthComponent>();
            if (playerHealth != null && playerHealth.IsDead) return true; // already dead, skip

            var damageable = hit.collider.GetComponent<IDamageable>();
            damageable?.TakeDamage(new DamageData(damage, DamageType.Combat, gameObject, hit.point));

            // Mirror EnemyAttackController — trigger rescue proxy if killing blow
            if (playerHealth != null && playerHealth.IsDead)
            {
                Debug.Log($"[RaycastAttackLauncher] Killing blow on {hit.collider.name}");
                hit.collider.GetComponent<PlayerDeathRescueProxy>()?.Activate(_owner);
            }
        }

        return true;
    }

    public void SetStats(float range, float cd, float dmg)
    {
        attackRange = range;
        cooldown = cd;
        damage = dmg;
    }
}