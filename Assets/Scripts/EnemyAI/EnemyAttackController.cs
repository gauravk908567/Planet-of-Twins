using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask enemyLayer;

    private IAnimationController _animController;
    private AttackRangeIndicator _rangeIndicator;

    private float _attackCooldown = 1.5f;
    private float _lastAttackTime;
    private bool _isAttacking;
    private float _damageMultiplier = 1f;
    private float _attackSlowdownMultiplier = 1f;
    private bool _isPossessedAttack;

    // ── Ranged state ──────────────────────────────────────────
    private bool _isRanged;
    private bool _useProjectile;
    private GameObject _projectilePrefab;
    private Transform _firePoint;
    private float _projectileSpeed = 14f;

    private readonly Collider[] _hitBuffer = new Collider[10];

    public void SetDamageMultiplier(float m) => _damageMultiplier = m;
    public void ClearDamageMultiplier() => _damageMultiplier = 1f;
    public void SetAttackSlowdown(float m) => _attackSlowdownMultiplier = Mathf.Max(1f, m);
    public void ClearAttackSlowdown() => _attackSlowdownMultiplier = 1f;

    private void Awake()
    {
        _animController = GetComponent<IAnimationController>();
        _rangeIndicator = GetComponentInChildren<AttackRangeIndicator>();
    }

    // ── Called by RangedEnemy.ApplyData ───────────────────────
    public void SetRangedMode(bool useProjectile, GameObject projectilePrefab,
                              Transform firePoint, float projectileSpeed)
    {
        _isRanged = true;
        _useProjectile = useProjectile;
        _projectilePrefab = projectilePrefab;
        _firePoint = firePoint;
        _projectileSpeed = projectileSpeed;
    }

    // ── Melee path — called by EnemyAttackState ───────────────
    public void TryAttack(bool isPossessed = false)
    {
        if (_isAttacking) return;
        if (Time.time < _lastAttackTime + _attackCooldown * _attackSlowdownMultiplier) return;

        _isAttacking = true;
        _isPossessedAttack = isPossessed;
        _animController?.PlayAttack();
        _lastAttackTime = Time.time;
    }

    // ── Ranged path — called by RangedAttackState ─────────────
    public void TryRangedAttack(Transform target)
    {
        if (_isAttacking) return;
        if (Time.time < _lastAttackTime + _attackCooldown * _attackSlowdownMultiplier) return;

        _isAttacking = true;
        _lastAttackTime = Time.time;

        if (_useProjectile)
            FireProjectile(target);
        else
            ExecuteRaycast(target);
    }

    // ── Called by animation event (melee only) ────────────────
    public void ExecuteHitDetection()
    {
        _rangeIndicator?.Show(attackRange);
        LayerMask targetLayer = _isPossessedAttack ? enemyLayer : playerLayer;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, attackRange, _hitBuffer, targetLayer);

        for (int i = 0; i < hitCount; i++)
        {
            if (_isPossessedAttack)
            {
                var possessable = _hitBuffer[i].GetComponent<IPossessable>();
                possessable?.OnHitByPossessed(GetComponent<Enemy>());
            }

            var playerHealth = _hitBuffer[i].GetComponent<PlayerHealthComponent>();
            if (playerHealth != null && playerHealth.IsDead) continue;

            var damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(new DamageData(
                attackDamage * _damageMultiplier,
                DamageType.Combat,
                gameObject,
                _hitBuffer[i].transform.position));

            if (playerHealth != null && playerHealth.IsDead)
            {
                Debug.Log($"[EnemyAttack] Melee killing blow on {_hitBuffer[i].name}");
                _hitBuffer[i].GetComponent<PlayerDeathRescueProxy>()
                             ?.Activate(GetComponent<Enemy>());
            }
        }

        if (_isPossessedAttack && hitCount == 0)
        {
            GetComponent<IDamageable>()?.TakeDamage(new DamageData(
                attackDamage * _damageMultiplier,
                DamageType.Combat, gameObject, transform.position));
        }

        _rangeIndicator?.Hide();
        _isAttacking = false;
    }

    // ── Called by Arrow when it collides with something ───────
    public void OnProjectileHit(Collider hit)
    {
        ApplyDamageToTarget(hit);
    }

    // ── Shared damage pipeline ────────────────────────────────
    private void ApplyDamageToTarget(Collider col)
    {
        var playerHealth = col.GetComponent<PlayerHealthComponent>();
        if (playerHealth != null && playerHealth.IsDead) return;

        var damageable = col.GetComponent<IDamageable>();
        if (damageable == null) return;

        damageable.TakeDamage(new DamageData(
            attackDamage * _damageMultiplier,
            DamageType.Combat,
            gameObject,
            col.transform.position));

        if (playerHealth != null && playerHealth.IsDead)
        {
            Debug.Log($"[EnemyAttack] Ranged killing blow on {col.name}");
            col.GetComponent<PlayerDeathRescueProxy>()?.Activate(GetComponent<Enemy>());
        }
    }

    // ── Ranged internals ──────────────────────────────────────
    private void FireProjectile(Transform target)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError($"[EnemyAttackController] {name} — projectilePrefab not assigned", this);
            _isAttacking = false;
            return;
        }

        Transform origin = _firePoint != null ? _firePoint : transform;
        Vector3 dir = (target.position - origin.position).normalized;

        GameObject proj = Instantiate(_projectilePrefab,
            origin.position, Quaternion.LookRotation(dir));

        var arrow = proj.GetComponent<Arrow>();
        if (arrow == null)
        {
            Debug.LogError($"[EnemyAttackController] Projectile prefab missing Arrow component", this);
            _isAttacking = false;
            return;
        }

        // Arrow reports hit back to this controller — controller handles all damage
        arrow.Initialise(dir, _projectileSpeed, this);

        // Clear immediately after launch — cooldown (_lastAttackTime) gates next shot.
        // If we wait for OnProjectileHit, a miss or out-of-layer hit locks firing forever.
        _isAttacking = false;
    }

    private void ExecuteRaycast(Transform target)
    {
        Transform origin = _firePoint != null ? _firePoint : transform;
        Vector3 dir = (target.position - origin.position).normalized;

        if (Physics.Raycast(origin.position, dir, out RaycastHit hit, attackRange, playerLayer))
            ApplyDamageToTarget(hit.collider);

        _isAttacking = false;
    }

    public void SetStats(float range, float damage, float cooldown, float windup)
    {
        attackRange = range;
        attackDamage = damage;
        _attackCooldown = cooldown;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _isRanged ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}