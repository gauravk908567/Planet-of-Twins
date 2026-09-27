using CommonCore;
using System.Collections;
using PoT.Fx;
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

    // The shared attack animation carries the melee OnAttackHitFrame event, so a ranged/projectile
    // attack also receives ExecuteHitDetection at the hit frame. Without this flag the melee
    // overlap-sphere ran with the ranged attackRange (7-10m) and damaged the player AT FIRE TIME
    // while the arrow was still flying (BUG-047). Set per ranged attack; eats exactly one hit-frame.
    private bool _suppressMeleeHitFrame;

    [Tooltip("Forward offset from the fire point when spawning projectiles, so the arrow clears the enemy's own collider.")]
    [SerializeField] private float _muzzleClearance = 0.75f;

    private Enemy _enemy;

    // ── Ranged state ──────────────────────────────────────────
    private bool _isRanged;
    private bool _useProjectile;
    private GameObject _projectilePrefab;
    private Transform _firePoint;
    private float _projectileSpeed = 14f;

    /// <summary>True when this enemy's basic attack is a projectile (its EnemyData carries one). Such an enemy has no
    /// melee attack: it shoots twins AND enemies, and keeps its distance while doing so (BUG-145).</summary>
    public bool FiresProjectiles => _useProjectile && _projectilePrefab != null;

    private readonly Collider[] _hitBuffer = new Collider[10];

    public void SetDamageMultiplier(float m) => _damageMultiplier = m;
    public void ClearDamageMultiplier() => _damageMultiplier = 1f;

    // POI-feed threshold buff (EnemyDarkEnergy) — its own slot so it COMPOSES with the shared
    // _damageMultiplier (Witness buff / GrandSummoner / ProximityPower) instead of stomping it.
    private float _poiBuffMultiplier = 1f;
    public void SetPoiBuff(float m) => _poiBuffMultiplier = Mathf.Max(0.1f, m);
    public void SetAttackSlowdown(float m) => _attackSlowdownMultiplier = Mathf.Max(1f, m);
    public void ClearAttackSlowdown() => _attackSlowdownMultiplier = 1f;

    private void Awake()
    {
        _animController = GetComponent<IAnimationController>();
        _rangeIndicator = GetComponentInChildren<AttackRangeIndicator>();
        _enemy = GetComponent<Enemy>();
    }

    // ── Called by RangedEnemy.ApplyData — marks the kiting archetype (gizmo/debug only) ──
    public void SetRangedMode() => _isRanged = true;

    // ── Called by Enemy.ApplyData (base) — projectile config comes from EnemyData, any archetype ──
    public void SetProjectile(bool useProjectile, GameObject projectilePrefab,
                              Transform firePoint, float projectileSpeed)
    {
        _useProjectile = useProjectile;
        _projectilePrefab = projectilePrefab;
        _firePoint = firePoint;
        _projectileSpeed = projectileSpeed;

        // Refcounted warm pool: the data's own prefab reference is the pool key — warmed while
        // this enemy lives, trimmed after the last user despawns (Enemy.OnDisable releases).
        if (useProjectile && projectilePrefab != null)
            _enemy?.RegisterPooledPrefab(projectilePrefab, PoolCategory.Projectiles);
    }

    public void ResetAttack()
    {
        _isAttacking = false;
        _isPossessedAttack = false;
        _targetEnemyLayer = false;
        _suppressMeleeHitFrame = false;
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
        // Data-driven projectile override — an enemy whose EnemyData carries a projectile fires it at its
        // current target instead of the melee overlap: at a twin, and at an enemy too (possession, clan war).
        // BUG-145: fighting an enemy it used to fall back to the melee overlap at its 7–8 m SHOOTING range, which
        // hit every enemy around it; a projectile enemy has no melee attack. No target = fall through to melee.
        if (FiresProjectiles && _enemy != null && _enemy.Target != null)
        {
            TryRangedAttack(_enemy.Target, isPossessed || targetEnemyLayer, isPossessed);
            return;
        }

        if (_isAttacking) return;
        if (Time.time < _lastAttackTime + _attackCooldown * _attackSlowdownMultiplier) return;

        _isAttacking = true;
        _isPossessedAttack = isPossessed;
        _targetEnemyLayer = isPossessed || targetEnemyLayer;
        _suppressMeleeHitFrame = false;   // fresh melee attack — a stale ranged flag must not eat its hit frame
        FaceTarget(_enemy != null ? _enemy.Target : null);   // commit the swing toward the target
        _animController?.PlayAttack();
        _enemy?.PlayMeleeAttackCue();   // archetype basic-attack VFX (EnemyVfxLibrary, R4)
        _lastAttackTime = Time.time;
    }

    // ── Ranged path ────────────────────────────────────────────
    /// <param name="atEnemy">The target is another enemy (possession, clan war): a projectile then hits the enemy
    ///   layer instead of the twins, never the shooter itself. The raycast mode only ever aims at twins.</param>
    /// <param name="possessedShot">Fired by a possessed enemy: full damage (no clan-war cut), and the enemy it hits
    ///   turns on the shooter, as a possessed melee hit does.</param>
    public void TryRangedAttack(Transform target, bool atEnemy = false, bool possessedShot = false)
    {
        if (_isAttacking) return;
        if (Time.time < _lastAttackTime + _attackCooldown * _attackSlowdownMultiplier) return;

        _isAttacking = true;
        _suppressMeleeHitFrame = true;   // ranged attack — the anim's melee hit-frame must not run
        _lastAttackTime = Time.time;
        FaceTarget(target);              // aim the body (and the firePoint) at the target before firing
        _animController?.PlayAttack();
        _enemy?.PlayRangedAttackCue(_firePoint);   // archetype basic-attack VFX (EnemyVfxLibrary, R4)

        if (_useProjectile)
            FireProjectile(target, atEnemy, possessedShot);
        else
            ExecuteRaycast(target);
    }

    /// <summary>Y-only snap toward the target at attack commit (user playtest 2026-07-10: enemies
    /// attacked while facing elsewhere — wrong swing reads AND directional cues like the grab
    /// soul-drain aimed the wrong way). Snap, not a turn: NavMeshAgent steering resumes after.</summary>
    private void FaceTarget(Transform target)
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ── Called by animation event (melee only) ────────────────
    public void ExecuteHitDetection()
    {
        // Ranged/projectile attack: damage belongs to the arrow's impact (OnProjectileHit) or the
        // raycast — never the melee overlap. Consume the hit-frame and bail.
        if (_suppressMeleeHitFrame)
        {
            _suppressMeleeHitFrame = false;
            _isAttacking = false;
            return;
        }

        _rangeIndicator?.Show(attackRange);
        LayerMask targetLayer = _targetEnemyLayer ? enemyLayer : playerLayer;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, attackRange, _hitBuffer, targetLayer);

        int landed = 0;
        for (int i = 0; i < hitCount; i++)
        {
            // Scanning the enemy layer finds this enemy's own colliders too: never hit yourself (BUG-145). Only
            // hits on OTHERS count, so the possessed-miss rule below fires on a real miss.
            if (_hitBuffer[i].GetComponentInParent<EnemyAttackController>() == this) continue;
            landed++;

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
                attackDamage * GetOutgoingDamageMultiplier(_hitBuffer[i], _isPossessedAttack),
                DamageType.Combat,
                gameObject,
                _hitBuffer[i].transform.position));

            FactionEnergySystem.Instance?.OnTwinTookDamage();

            // Shared hit spark ON THE PLAYER (Common book) — only when an enemy strikes a twin,
            // and never while a held state owns the visuals (GroupGrab absorb — no slash spark).
            if (playerHealth != null && !(_enemy != null && _enemy.SuppressBasicAttackCues))
                CommonFx.Play(FxIds.Common.Effects.on_hiteffect,
                    new CueContext(_hitBuffer[i].transform.position));

            if (playerHealth != null && playerHealth.IsDead)
            {
                _hitBuffer[i].GetComponentInParent<PlayerDeathRescueProxy>()
                             ?.Activate(GetComponent<Enemy>());
            }
        }

        // Possessed miss — self damage
        if (_isPossessedAttack && landed == 0)
        {
            GetComponent<IDamageable>()?.TakeDamage(new DamageData(
                attackDamage * _damageMultiplier,
                DamageType.Combat, gameObject, transform.position));
        }

        _rangeIndicator?.Hide();
        _isAttacking = false;
    }

    // ── Called by Arrow when it collides ─────────────────────
    public void OnProjectileHit(Collider hit, bool possessedShot = false)
    {
        // A possessed enemy's arrow makes the enemy it hits turn on the shooter, as a possessed melee hit does.
        if (possessedShot) hit.GetComponentInParent<IPossessable>()?.OnHitByPossessed(_enemy);
        ApplyDamageToTarget(hit, possessedShot);
        FactionEnergySystem.Instance?.OnTwinTookDamage();
    }

    // ── Shared damage pipeline ────────────────────────────────
    private void ApplyDamageToTarget(Collider col, bool possessedAttack = false)
    {
        var playerHealth = col.GetComponentInParent<PlayerHealthComponent>();
        if (playerHealth != null && playerHealth.IsDead) return;

        var damageable = col.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        damageable.TakeDamage(new DamageData(
            attackDamage * GetOutgoingDamageMultiplier(col, possessedAttack),
            DamageType.Combat,
            gameObject,
            col.transform.position));

        // Shared hit spark ON THE PLAYER (Common book) — ranged/projectile strike on a twin.
        if (playerHealth != null)
            CommonFx.Play(FxIds.Common.Effects.on_hiteffect, new CueContext(col.transform.position));

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
    // possessedAttack is passed in, not read from _isPossessedAttack: an arrow lands after the attack that fired it.
    private float GetOutgoingDamageMultiplier(Collider target, bool possessedAttack)
    {
        float m = _damageMultiplier * _poiBuffMultiplier;

        if (!possessedAttack && target.GetComponentInParent<Enemy>() != null)
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
    private void FireProjectile(Transform target, bool atEnemy, bool possessedShot)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError($"[EnemyAttackController] {name} — projectilePrefab not assigned", this);
            _isAttacking = false;
            return;
        }

        Transform origin = _firePoint != null ? _firePoint : transform;
        Vector3 dir = (target.position - origin.position).normalized;

        // Spawn with forward clearance: fire points sit at/inside the body, so arrows spawned
        // exactly there start embedded in the enemy's own collider and can visibly "stick".
        Vector3 spawnPos = origin.position + dir * _muzzleClearance;

        GameObject proj = GameplayPool.Spawn(_projectilePrefab,
            PoolCategory.Projectiles, spawnPos, Quaternion.LookRotation(dir));

        var arrow = proj.GetComponent<Arrow>();
        if (arrow == null)
        {
            Debug.LogError($"[EnemyAttackController] Projectile prefab missing Arrow component", this);
            _isAttacking = false;
            return;
        }

        arrow.Initialise(dir, _projectileSpeed, this, atEnemy ? enemyLayer : (LayerMask?)null, possessedShot);
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