using CommonCore;
using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    private float attackRange;
    private float attackDamage;
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

    // ── Attack context flags ──────────────────────────────────
    // _isPossessedAttack: true ONLY when this enemy is possessed — drives
    //   damage calc (bypasses clan war reduction, triggers possessed hit reaction).
    // _targetEnemyLayer: true when hit detection should scan the enemy layer
    //   (possession AND clan war both need this, but only possession is "possessed").
    private bool _isPossessedAttack;
    private bool _targetEnemyLayer;

    private Enemy _enemy;

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
        _enemy = GetComponent<Enemy>();
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

    public void ResetAttack()
    {
        _isAttacking = false;
        _isPossessedAttack = false;
        _targetEnemyLayer = false;
        StopAllCoroutines();
    }

    // ── Melee path ─────────────────────────────────────────────
    /// <param name="isPossessed">True only when this enemy is possessed —
    ///   drives damage reduction bypass and hit-reaction logic.</param>
    /// <param name="targetEnemyLayer">True when hit detection should scan
    ///   the enemy layer. Pass true for both possession AND clan war attacks.
    ///   Separate from isPossessed so clan war reduction fires correctly.</param>
    public void TryAttack(bool isPossessed = false, bool targetEnemyLayer = false)
    {
        if (_isAttacking) return;
        if (Time.time < _lastAttackTime + _attackCooldown * _attackSlowdownMultiplier) return;

        _isAttacking = true;
        _isPossessedAttack = isPossessed;
        _targetEnemyLayer = isPossessed || targetEnemyLayer;
        _animController?.PlayAttack();
        _lastAttackTime = Time.time;
    }

    // ── Ranged path ────────────────────────────────────────────
    public void TryRangedAttack(Transform target)
    {
        if (_isAttacking) return;
        if (Time.time < _lastAttackTime + _attackCooldown * _attackSlowdownMultiplier) return;

        _isAttacking = true;
        _lastAttackTime = Time.time;
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
        LayerMask targetLayer = _targetEnemyLayer ? enemyLayer : playerLayer;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, attackRange, _hitBuffer, targetLayer);

        for (int i = 0; i < hitCount; i++)
        {
            if (_isPossessedAttack)
            {
                var possessable = _hitBuffer[i].GetComponent<IPossessable>();
                possessable?.OnHitByPossessed(GetComponent<Enemy>());
            }

            var playerHealth = _hitBuffer[i].GetComponentInParent<PlayerHealthComponent>();
            if (playerHealth != null && playerHealth.IsDead) continue;

            var damageable = _hitBuffer[i].GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(new DamageData(
                attackDamage * GetOutgoingDamageMultiplier(_hitBuffer[i]),
                DamageType.Combat,
                gameObject,
                _hitBuffer[i].transform.position));

            FactionEnergySystem.Instance?.OnTwinTookDamage();

            if (playerHealth != null && playerHealth.IsDead)
            {
                _hitBuffer[i].GetComponentInParent<PlayerDeathRescueProxy>()
                             ?.Activate(GetComponent<Enemy>());
            }
        }

        // Possessed miss — self damage
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
        FactionEnergySystem.Instance?.OnTwinTookDamage();
    }

    // ── Shared damage pipeline ────────────────────────────────
    private void ApplyDamageToTarget(Collider col)
    {
        var playerHealth = col.GetComponentInParent<PlayerHealthComponent>();
        if (playerHealth != null && playerHealth.IsDead) return;

        var damageable = col.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        damageable.TakeDamage(new DamageData(
            attackDamage * GetOutgoingDamageMultiplier(col),
            DamageType.Combat,
            gameObject,
            col.transform.position));

        if (playerHealth != null && playerHealth.IsDead)
            col.GetComponentInParent<PlayerDeathRescueProxy>()?.Activate(GetComponent<Enemy>());
    }

    /// <summary>
    /// Computes final outgoing damage multiplier.
    /// Clan war reduction (0.3x) only fires when:
    ///   - Target is an Enemy
    ///   - ClanWarActive on shared BB
    ///   - This is NOT a possession attack (possessed enemies fight at full damage)
    /// Future multipliers (buff auras, elemental resist) drop in here.
    /// </summary>
    private float GetOutgoingDamageMultiplier(Collider target)
    {
        float m = _damageMultiplier;

        if (!_isPossessedAttack && target.GetComponentInParent<Enemy>() != null)
        {
            var shared = BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
            if (shared != null &&
                shared.TryGet(PoTNames.ClanWarActive, out bool cw, false) && cw)
            {
                m *= _enemy?.Data?.clanWarDamageMultiplier ?? 0.3f;
            }
        }

        return m;
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
        _isAttacking = false;
    }

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