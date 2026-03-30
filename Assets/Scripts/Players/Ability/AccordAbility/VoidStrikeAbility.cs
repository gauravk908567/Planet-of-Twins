using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kai's accord Q — Void Strike.
///
/// FLOW:
///   Activation → for _duration seconds, spawn a hazard point every _spawnInterval.
///   Max _maxPoints active at once — oldest point removed when cap is reached.
///   Each point damages enemies inside _pointRadius for _damagePerTick every tick.
///   Points spawn within _spawnRadius of the caster, at least _minDistance away.
///
/// UPGRADEABLE:
///   _maxPoints and _duration passed via constructor — SO upgrades just change these values.
/// </summary>
public class VoidStrikeAbility : IAbility, IAbilityHUDSource, IAbilityActiveState
{
    private readonly MonoBehaviour _runner;
    private readonly LayerMask _enemyLayer;
    private readonly float _spawnRadius;
    private readonly float _minDistance;
    private readonly float _spawnInterval;
    private readonly float _duration;
    private readonly float _pointRadius;
    private readonly float _damagePerTick;
    private readonly float _tickInterval;
    private readonly float _cooldown;
    private readonly int _maxPoints;
    private readonly GameObject _pointPrefab;

    private float _lastUseTime = -999f;
    private bool _isActive;

    // ── IAbility ──────────────────────────────────────────────
    public void Initialize(GameObject owner) { }
    public void Tick() { }

    // ── IAbilityHUDSource ─────────────────────────────────────
    public string AbilityName => "";
    public int CurrentCharges => 1;
    public int MaxCharges => 1;
    public bool IsActive => _isActive;
    public float CooldownProgress
    {
        get
        {
            if (_isActive) return 1f;
            float elapsed = Time.time - _lastUseTime;
            return _cooldown > 0f ? Mathf.Clamp01(elapsed / _cooldown) : 1f;
        }
    }

    // ── IAbilityActiveState ───────────────────────────────────
    public bool IsAbilityActive => _isActive;

    // ── Constructor ───────────────────────────────────────────
    public VoidStrikeAbility(
        MonoBehaviour runner,
        LayerMask enemyLayer,
        float spawnRadius = 5f,
        float minDistance = 1.2f,
        float spawnInterval = 0.25f,
        float duration = 5f,
        float pointRadius = 0.8f,
        float damagePerTick = 1.25f,
        float tickInterval = 0.25f,
        float cooldown = 5f,
        int maxPoints = 5,
        GameObject pointPrefab = null)
    {
        _runner = runner;
        _enemyLayer = enemyLayer;
        _spawnRadius = spawnRadius;
        _minDistance = minDistance;
        _spawnInterval = spawnInterval;
        _duration = duration;
        _pointRadius = pointRadius;
        _damagePerTick = damagePerTick;
        _tickInterval = tickInterval;
        _cooldown = cooldown;
        _maxPoints = maxPoints;
        _pointPrefab = pointPrefab;
    }

    // ── Activation ────────────────────────────────────────────
    public void TryActivate()
    {
        if (_isActive) return;
        if (Time.time < _lastUseTime + _cooldown) return;

        _lastUseTime = Time.time;
        _runner.StartCoroutine(RunAbility());
    }

    // ── Main coroutine ────────────────────────────────────────
    private IEnumerator RunAbility()
    {
        _isActive = true;

        // Track active point positions + VFX instances for cleanup
        var activePoints = new Queue<Vector3>();
        var activeVFX = new Queue<GameObject>();

        float elapsed = 0f;
        float nextSpawn = 0f;
        float nextTick = 0f;

        while (elapsed < _duration)
        {
            // Spawn new point at interval
            if (elapsed >= nextSpawn)
            {
                Vector3 origin = _runner.transform.position;
                Vector3 point = PickRandomPoint(origin);

                // If at cap, remove oldest point and its VFX
                if (activePoints.Count >= _maxPoints)
                {
                    activePoints.Dequeue();
                    var old = activeVFX.Dequeue();
                    if (old != null) Object.Destroy(old);
                }

                activePoints.Enqueue(point);

                // Spawn VFX at point
                GameObject vfx = null;
                if (_pointPrefab != null)
                {
                    vfx = Object.Instantiate(_pointPrefab, point,
                                             _pointPrefab.transform.rotation);
                    var ps = vfx.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        // Set shape radius to match damage radius
                        var shape = ps.shape;
                        shape.radius = _pointRadius;
                        ps.Play(true);
                    }
                }
                activeVFX.Enqueue(vfx);

                nextSpawn += _spawnInterval;
            }

            // Damage tick
            if (elapsed >= nextTick)
            {
                foreach (Vector3 pt in activePoints)
                {
                    Collider[] hits = Physics.OverlapSphere(pt, _pointRadius, _enemyLayer);
                    foreach (Collider hit in hits)
                        hit.GetComponent<EnemyHealthComponent>()?.TakeDamage(
                            new DamageData(_damagePerTick, DamageType.Ability));
                }
                nextTick += _tickInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Cleanup all remaining VFX
        foreach (var vfx in activeVFX)
            if (vfx != null) Object.Destroy(vfx);

        _isActive = false;
    }

    // ── Helpers ───────────────────────────────────────────────
    private Vector3 PickRandomPoint(Vector3 origin)
    {
        // Try to find a point within spawn radius but outside min distance
        for (int i = 0; i < 20; i++)
        {
            Vector2 rand = Random.insideUnitCircle * _spawnRadius;
            Vector3 point = origin + new Vector3(rand.x, 0f, rand.y);
            if (Vector3.Distance(origin, point) >= _minDistance)
                return point;
        }

        // Fallback — place at exactly min distance in random direction
        Vector2 dir = Random.insideUnitCircle.normalized;
        return origin + new Vector3(dir.x, 0f, dir.y) * _minDistance;
    }
}