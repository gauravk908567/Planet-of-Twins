using UnityEngine;

public class ProjectileAttackLauncher : MonoBehaviour, IProjectileLauncher
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Stats — set via RangedEnemyData through SetStats()")]
    [SerializeField] private float attackRange = 12f;
    [SerializeField] private float cooldown = 2f;
    [SerializeField] private float projectileSpeed = 14f;
    [SerializeField] private float damage = 10f;

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
        if (projectilePrefab == null)
        {
            Debug.LogError($"[ProjectileAttackLauncher] {gameObject.name} — projectilePrefab not assigned", this);
            return false;
        }

        _lastFireTime = Time.time;

        Transform origin = firePoint != null ? firePoint : transform;
        Vector3 dir = (target.position - origin.position).normalized;

        GameObject proj = Instantiate(projectilePrefab, origin.position, Quaternion.LookRotation(dir));

        // Inject movement
        proj.GetComponent<IProjectileData>()?.Initialise(dir, projectileSpeed);

        // Inject owner + damage so projectile can trigger death proxy on hit
        proj.GetComponent<ProjectileBase>()?.SetOwnerAndDamage(_owner, damage);

        return true;
    }

    public void SetStats(float range, float cd, float speed)
    {
        attackRange = range;
        cooldown = cd;
        projectileSpeed = speed;
        // damage comes from EnemyData.attackDamage — set separately
    }

    public void SetDamage(float dmg) => damage = dmg;
}