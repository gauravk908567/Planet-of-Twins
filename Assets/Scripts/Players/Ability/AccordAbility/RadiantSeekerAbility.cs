using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lyra's accord Q — Radiant Seeker.
/// Implements IAbilityHUDSource for cooldown ring.
/// Implements IAbilityActiveState: true while a live orb is in the scene.
/// Orb calls NotifyOrbDestroyed() on its own Destroy so this tracks correctly.
/// </summary>
public class RadiantSeekerAbility : IAbility, IAbilityHUDSource, IAbilityActiveState
{
    private readonly MonoBehaviour _runner;
    private readonly GameObject _orbPrefab;
    private readonly LayerMask _enemyLayer;
    private readonly float _possessionRadius;
    private readonly float _possessionDuration;
    private readonly float _reseekCooldown;
    private readonly float _cooldown;

    private float _lastUseTime = -999f;
    private bool _orbIsAlive = false;

    // ── IAbility ──────────────────────────────────────────────
    public void Initialize(GameObject owner) { }
    public void Tick() { }

    // ── IAbilityHUDSource ─────────────────────────────────────
    public string AbilityName => "Radiant Seeker";
    public int CurrentCharges => 1;
    public int MaxCharges => 1;
    public bool IsActive => _orbIsAlive;
    public float CooldownProgress
    {
        get
        {
            if (_orbIsAlive) return 1f;
            float elapsed = Time.time - _lastUseTime;
            return _cooldown > 0f ? Mathf.Clamp01(elapsed / _cooldown) : 1f;
        }
    }

    // ── IAbilityActiveState ───────────────────────────────────
    /// True while a live orb exists in the scene.
    /// AccordIconSlot defers the flip-back until this returns false.
    public bool IsAbilityActive => _orbIsAlive;

    // Called by RadiantSeekerOrb.OnDestroy so we know the orb is gone
    public void NotifyOrbDestroyed() => _orbIsAlive = false;

    public RadiantSeekerAbility(
        MonoBehaviour runner,
        GameObject orbPrefab,
        LayerMask enemyLayer,
        float possessionRadius = 2.5f,
        float possessionDuration = 2f,
        float reseekCooldown = 0.55f,
        float cooldown = 5f)
    {
        _runner = runner;
        _orbPrefab = orbPrefab;
        _enemyLayer = enemyLayer;
        _possessionRadius = possessionRadius;
        _possessionDuration = possessionDuration;
        _reseekCooldown = reseekCooldown;
        _cooldown = cooldown;
    }

    public void TryActivate()
    {
        if (Time.time < _lastUseTime + _cooldown) return;

        if (_orbPrefab == null)
        {
            Debug.LogError("[RadiantSeekerAbility] Orb prefab not assigned.");
            return;
        }

        Vector3 spawnPosition = _runner.transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPosition, out hit, 2f, NavMesh.AllAreas))
            spawnPosition = hit.position;
        else
            Debug.LogWarning("[RadiantSeekerAbility] No NavMesh point near Lyra.");

        _lastUseTime = Time.time;
        _orbIsAlive = true;

        var go = Object.Instantiate(_orbPrefab, spawnPosition, Quaternion.identity);
        var orb = go.GetComponent<RadiantSeekerOrb>();

        if (orb == null)
        {
            Debug.LogError("[RadiantSeekerAbility] Prefab missing RadiantSeekerOrb.");
            Object.Destroy(go);
            _orbIsAlive = false;
            return;
        }

        var agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.Warp(spawnPosition);

        // Pass this ability reference so orb can notify us on destroy
        orb.Initialise(_enemyLayer, _possessionRadius, _possessionDuration,
                       _reseekCooldown, this);
    }
}