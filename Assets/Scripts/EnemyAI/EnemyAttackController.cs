using System.Collections;
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
    private float _attackWindup = 0.3f;
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

    /// <summary>
    /// FIX: resets _isAttacking and stops any in-flight windup coroutine.
    /// Called from Enemy.ResetForPool() so pooled enemies aren't permanently
    /// locked out of attacking after being stunned or killed mid-windup.
    /// </summary>
    public void ResetAttack()
    {
        _isAttacking = false;
        StopAllCoroutines();
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

        // FIX: was missing — ranged enemies played no attack animation at all.
        // Cosmetic only for projectile path (arrow already in flight when anim plays).
        // For raycast path with windup, the animation covers the delay visually.
        _animController?.PlayAttack();

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

            // Use GetComponentInParent — collider may be on a child GO
            // while PlayerHealthComponent lives on the root Player GO
            var playerHealth = _hitBuffer[i].GetComponentInParent<PlayerHealthComponent>();
            if (playerHealth != null && playerHealth.IsDead) continue;

            var damageable = _hitBuffer[i].GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(new DamageData(
                attackDamage * _damageMultiplier,
                DamageType.Combat,
                gameObject,
                _hitBuffer[i].transform.position));

            if (playerHealth != null && playerHealth.IsDead)
            {
                Debug.Log($"[EnemyAttack] Melee killing blow on {_hitBuffer[i].name}");
                _hitBuffer[i].GetComponentInParent<PlayerDeathRescueProxy>()
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

    // ── Called by Arrow when it collides ─────────────────────
    public void OnProjectileHit(Collider hit)
    {
        ApplyDamageToTarget(hit);
    }

    // ── Shared damage pipeline ────────────────────────────────
    private void ApplyDamageToTarget(Collider col)
    {
        var playerHealth = col.GetComponentInParent<PlayerHealthComponent>();
        if (playerHealth != null && playerHealth.IsDead) return;

        var damageable = col.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        damageable.TakeDamage(new DamageData(
            attackDamage * _damageMultiplier,
            DamageType.Combat,
            gameObject,
            col.transform.position));

        if (playerHealth != null && playerHealth.IsDead)
        {
            Debug.Log($"[EnemyAttack] Ranged killing blow on {col.name}");
            col.GetComponentInParent<PlayerDeathRescueProxy>()?.Activate(GetComponent<Enemy>());
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

        arrow.Initialise(dir, _projectileSpeed, this);

        // Clear immediately after launch — a miss or out-of-layer hit
        // would lock firing forever if we waited for OnProjectileHit.
        _isAttacking = false;
    }

    /// <summary>
    /// Instant-hit raycast with optional windup delay driven by _attackWindup.
    /// _isAttacking remains true during the wait so no overlap is possible.
    /// ResetAttack() stops the coroutine if the enemy is stunned/pooled mid-windup.
    /// </summary>
    private void ExecuteRaycast(Transform target)
    {
        if (_attackWindup > 0f)
            StartCoroutine(DelayedRaycast(target));
        else
        {
            FireRaycastImmediate(target);
            _isAttacking = false;
        }
    }

    private IEnumerator DelayedRaycast(Transform target)
    {
        yield return new WaitForSeconds(_attackWindup);
        if (target != null)
            FireRaycastImmediate(target);
        _isAttacking = false;
    }

    private void FireRaycastImmediate(Transform target)
    {
        Transform origin = _firePoint != null ? _firePoint : transform;
        Vector3 dir = (target.position - origin.position).normalized;

        if (Physics.Raycast(origin.position, dir, out RaycastHit hit, attackRange, playerLayer))
            ApplyDamageToTarget(hit.collider);
    }

    // ── Called by Enemy.ApplyData ─────────────────────────────
    public void SetStats(float range, float damage, float cooldown, float windup)
    {
        attackRange = range;
        attackDamage = damage;
        _attackCooldown = cooldown;
        _attackWindup = windup;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _isRanged ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}