using System.Collections;
using UnityEngine;

/// <summary>
/// Self-contained bomb projectile — shared by Witness, Resonant, Tether-Breaker.
///
/// FLOW:
///   Instantiate → call Initialise() → bomb sits at position for detonationDelay
///   → detonates → applies BombEffectData to all hits → destroys self.
///
/// SETUP:
///   Prefab: root GO + BombProjectile + optional particle child (auto-plays).
///   Add a visual timer ring as child — set it up in the prefab.
///   No owner reference needed — fire and forget after Initialise().
///
/// USAGE:
///   var bomb = Instantiate(bombPrefab, position, rotation)
///              .GetComponent<BombProjectile>();
///   bomb.Initialise(detonationDelay, aoeRadius, effectData,
///                   playerLayer, enemyLayer);
/// </summary>
public class BombProjectile : MonoBehaviour
{
    [Header("Defaults — overridden by Initialise()")]
    [SerializeField] private float _detonationDelay = 1.25f;
    [SerializeField] private float _aoeRadius = 1.5f;
    [SerializeField] private BombEffectData _effectData;

    [Header("VFX")]
    [Tooltip("Optional timer ring child — scale X from 1 to 0 over detonation delay.")]
    [SerializeField] private Transform _timerRing;
    [Tooltip("Optional particle on detonation.")]
    [SerializeField] private GameObject _detonationVFXPrefab;

    private LayerMask _playerLayer;
    private LayerMask _enemyLayer;
    private bool _initialised;

    // ── Public API ────────────────────────────────────────────
    public void Initialise(
        float detonationDelay,
        float aoeRadius,
        BombEffectData effectData,
        LayerMask playerLayer,
        LayerMask enemyLayer)
    {
        _detonationDelay = detonationDelay;
        _aoeRadius = aoeRadius;
        _effectData = effectData;
        _playerLayer = playerLayer;
        _enemyLayer = enemyLayer;
        _initialised = true;

        StartCoroutine(DetonationRoutine());
    }

    /// <summary>
    /// Alternative to Initialise() — rolls the bomb from spawnPos toward targetPos
    /// along the ground, then detonates on arrival.
    /// The bomb travels for travelDuration seconds then sits and counts down detonationDelay.
    /// Call after Instantiate() instead of Initialise().
    /// </summary>
    public void Roll(
        Vector3 spawnPos,
        Vector3 targetPos,
        float travelDuration,
        float detonationDelay,
        float aoeRadius,
        BombEffectData effectData,
        LayerMask playerLayer,
        LayerMask enemyLayer)
    {
        _detonationDelay = detonationDelay;
        _aoeRadius = aoeRadius;
        _effectData = effectData;
        _playerLayer = playerLayer;
        _enemyLayer = enemyLayer;
        _initialised = true;

        transform.position = spawnPos;
        StartCoroutine(RollRoutine(spawnPos, targetPos, travelDuration));
    }

    private IEnumerator RollRoutine(Vector3 from, Vector3 to, float travelDuration)
    {
        float elapsed = 0f;
        float groundY = from.y;

        // Direction of roll — fixed at throw time (straight line, not tracking)
        Vector3 rollDir = (new Vector3(to.x, 0f, to.z) - new Vector3(from.x, 0f, from.z)).normalized;
        float rollSpeed = Vector3.Distance(
            new Vector3(from.x, 0f, from.z),
            new Vector3(to.x, 0f, to.z)) / travelDuration;

        // Hide timer ring while rolling — only shows during detonation countdown
        if (_timerRing != null) _timerRing.localScale = Vector3.zero;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;

            // Move in roll direction, stay on ground Y
            Vector3 next = transform.position + rollDir * rollSpeed * Time.deltaTime;
            next.y = groundY;

            // Simple wall bounce — reflect off obstacles via raycast
            Ray ray = new Ray(transform.position, rollDir);
            if (Physics.Raycast(ray, out RaycastHit hit, rollSpeed * Time.deltaTime + 0.1f,
                                 ~(_playerLayer | _enemyLayer))) // ignore player/enemy layers
            {
                // Reflect direction off surface normal
                rollDir = Vector3.Reflect(rollDir, hit.normal);
                rollDir.y = 0f;
                rollDir.Normalize();
                next = transform.position + rollDir * rollSpeed * Time.deltaTime;
                next.y = groundY;
            }

            transform.position = next;

            // Spin to sell the roll visually
            transform.Rotate(Vector3.right, 720f * Time.deltaTime, Space.World);

            yield return null;
        }

        // Roll timer complete — now start detonation countdown
        if (_timerRing != null) _timerRing.localScale = Vector3.one;
        StartCoroutine(DetonationRoutine());
    }

    private System.Collections.IEnumerator Start()
    {
        // Wait one frame so Roll()/Initialise() called after Instantiate() can set _initialised
        // before we fall back to Inspector defaults
        yield return null;
        if (!_initialised)
            StartCoroutine(DetonationRoutine());
    }

    // ── Detonation ────────────────────────────────────────────
    private IEnumerator DetonationRoutine()
    {
        float elapsed = 0f;

        while (elapsed < _detonationDelay)
        {
            elapsed += Time.deltaTime;

            // Animate timer ring (scale X from 1→0)
            if (_timerRing != null)
            {
                float t = 1f - Mathf.Clamp01(elapsed / _detonationDelay);
                _timerRing.localScale = new Vector3(t, 1f, t);
            }

            yield return null;
        }

        Detonate();
    }

    private void Detonate()
    {
        if (_effectData == null)
        {
            Debug.LogWarning("[BombProjectile] No BombEffectData assigned — no effects applied.");
            Destroy(gameObject);
            return;
        }

        // Detonation VFX
        if (_detonationVFXPrefab != null)
        {
            var vfx = Instantiate(_detonationVFXPrefab, transform.position, Quaternion.identity);
            var ps = vfx.GetComponent<ParticleSystem>();
            Destroy(vfx, ps != null ? ps.main.duration + 0.1f : 1f);
        }

        // Hit players — deduplicate across multiple colliders on same player
        var playerHits = Physics.OverlapSphere(transform.position, _aoeRadius, _playerLayer);
        var hitPlayers = new System.Collections.Generic.HashSet<Player>();
        foreach (var hit in playerHits)
        {
            var p = hit.GetComponent<Player>() ?? hit.GetComponentInParent<Player>();
            if (p != null && !(p is SoulPlayer) && hitPlayers.Add(p))
                ApplyEffectsToPlayer(hit);
        }

        // Hit enemies (if configured)
        if (_effectData.affectsEnemies)
        {
            var enemyHits = Physics.OverlapSphere(transform.position, _aoeRadius, _enemyLayer);
            foreach (var hit in enemyHits)
                ApplyEffectsToEnemy(hit);
        }

        Destroy(gameObject);
    }

    // ── Effect application ─────────────────────────────────────
    private void ApplyEffectsToPlayer(Collider hit)
    {
        var player = hit.GetComponent<Player>() ?? hit.GetComponentInParent<Player>();
        if (player == null || player is SoulPlayer) return;

        // Damage — capped to never kill (bomb bypasses PlayerDeathRescueProxy)
        // Lethal hits must go through the rescue system, not direct TakeDamage
        if (_effectData.damage > 0f && player.Health != null)
        {
            float safeDamage = Mathf.Min(_effectData.damage,
                                          player.Health.CombatHealth - 1f);
            if (safeDamage > 0f)
                player.Health.TakeDamage(new DamageData(safeDamage, _effectData.damageType));
        }

        // Movement slow — applied to player via ISlowReceiver
        if (_effectData.slowDuration > 0f)
        {
            var slowReceiver = player.GetComponent<ISlowReceiver>()
                            ?? player.GetComponentInParent<ISlowReceiver>();
            if (slowReceiver != null)
            {
                slowReceiver.ApplySlow(_effectData.slowMultiplier,
                                       _effectData.slowDuration, "bomb_slow");
                Debug.Log($"[BombProjectile] Slow applied to {player.name} " +
                          $"mult={_effectData.slowMultiplier} dur={_effectData.slowDuration}");
            }
            else
                Debug.LogWarning($"[BombProjectile] {player.name} has no ISlowReceiver — slow skipped.");
        }

        // Ability suppression
        var abilityCtrl = player.GetComponent<AbilityController>();
        if (abilityCtrl != null && _effectData.suppressionDuration > 0f)
        {
            bool suppress = _effectData.suppressAllAbilities || _effectData.suppressPrimaryOnly;
            if (suppress)
            {
                AbilitySuppressionEffect.Apply(abilityCtrl, _effectData.suppressionDuration,
                                               suppressPrimaryOnly: !_effectData.suppressAllAbilities,
                                               runner: this);
                Debug.Log($"[BombProjectile] Suppression applied to {player.name} " +
                          $"duration={_effectData.suppressionDuration} " +
                          $"allAbilities={_effectData.suppressAllAbilities}");
            }
        }
        else if (abilityCtrl == null)
            Debug.LogWarning($"[BombProjectile] {player.name} has no AbilityController — suppression skipped.");

        // Pushback — via ApplyExternalForce on PlayerMovementController
        if (_effectData.pushbackForce > 0f)
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            dir.y = 0f;
            var movement = player.GetComponent<PlayerMovementController>();
            if (movement != null)
                movement.ApplyExternalForce(dir * _effectData.pushbackForce);
        }
    }

    private void ApplyEffectsToEnemy(Collider hit)
    {
        var enemy = hit.GetComponent<Enemy>() ?? hit.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        // Damage
        if (_effectData.damage > 0f)
            enemy.Health?.TakeDamage(new DamageData(_effectData.damage, _effectData.damageType));

        // Slow
        if (_effectData.slowDuration > 0f)
            (enemy as ISlowReceiver)?.ApplySlow(_effectData.slowMultiplier,
                                                 _effectData.slowDuration,
                                                 "bomb_slow");

        // Pushback
        if (_effectData.pushbackForce > 0f)
        {
            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            dir.y = 0f;
            enemy.ReceiveKnockback(new KnockbackData(dir * _effectData.pushbackForce,
                                                      KnockbackSource.Bomb));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, _aoeRadius);
    }
}