using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AccordSpiritAgent : MonoBehaviour
{
    private NavMeshAgent _agent;
    private LayerMask _enemyLayer;
    private float _cleansePulseRadius;
    private float _portalDamagePerSec;
    private float _portalDuration;
    private float _portalRadius;
    private float _maxSeekDuration = 10f;

    private Transform _target;
    private List<Transform> _claimedTargets; // targets claimed by other spirits
    private bool _arrived;
    private bool _initialised;
    private float _seekTimer;
    private Vector3 _spawnPosition;

    [SerializeField] private float _maxHealth = 0f;
    private float _currentHealth;
    private bool _destroyed;

    // VFX
    private GameObject _arrivalChargeEffectPrefab;
    private GameObject _ringParticlePrefab;

    public void Initialise(
        LayerMask enemyLayer,
        float cleansePulseRadius,
        float portalDamagePerSec,
        float portalDuration,
        float portalRadius,
        AccordSpiritSystem owner,
        List<Transform> claimedTargets,
        GameObject arrivalChargeEffectPrefab,
        GameObject ringParticlePrefab)
    {
        _enemyLayer = enemyLayer;
        _cleansePulseRadius = cleansePulseRadius;
        _portalDamagePerSec = portalDamagePerSec;
        _portalDuration = portalDuration;
        _portalRadius = portalRadius;
        _claimedTargets = claimedTargets;
        _arrivalChargeEffectPrefab = arrivalChargeEffectPrefab;
        _ringParticlePrefab = ringParticlePrefab;
        _agent = GetComponent<NavMeshAgent>();
        _initialised = true;
        _seekTimer = 0f;
        _spawnPosition = transform.position;
        _currentHealth = _maxHealth;
        _destroyed = false;

        SeekTarget();
    }

    public void TakeSpiritDamage(float amount)
    {
        if (_maxHealth <= 0f || _destroyed) return;
        _currentHealth -= amount;
        if (_currentHealth <= 0f)
        {
            _destroyed = true;
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_maxHealth <= 0f || _destroyed) return;
        var enemy = other.GetComponent<Enemy>();
        if (enemy == null || enemy.Data == null) return;
        if (!enemy.Data.canDamageSpirits) return;
        TakeSpiritDamage(enemy.Data.spiritDamage);
    }

    private void Update()
    {
        if (!_initialised || _arrived || _destroyed) return;

        if (_target != null && !_target.gameObject.activeInHierarchy)
        {
            _claimedTargets?.Remove(_target);
            _target = null;
        }

        if (_target == null)
        {
            _seekTimer += Time.deltaTime;
            if (_seekTimer >= _maxSeekDuration) { Destroy(gameObject); return; }
            if (Time.frameCount % 30 == 0) SeekTarget();
            return;
        }

        _agent.SetDestination(_target.position);

        if (_agent.pathPending) return;
        if (!_agent.hasPath) return;

        // Guard: remainingDistance returns Infinity when agent is off NavMesh
        if (float.IsInfinity(_agent.remainingDistance) || float.IsNaN(_agent.remainingDistance))
        {
            // Re-warp and re-path
            _agent.Warp(transform.position);
            if (_target != null) _agent.SetDestination(_target.position);
            return;
        }

        float stopping = Mathf.Max(_agent.stoppingDistance, 1.0f);
        if (_agent.remainingDistance <= stopping)
        {
            _arrived = true;
            _agent.enabled = false;
            _claimedTargets?.Remove(_target);
            StartCoroutine(ExecutePhases());
        }
    }

    private IEnumerator ExecutePhases()
    {
        yield return StartCoroutine(PhaseOneCleanse());
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(PhaseTwoPortal());
        Destroy(gameObject);
    }

    private IEnumerator PhaseOneCleanse()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, _cleansePulseRadius, _enemyLayer);
        foreach (Collider hit in hits)
            hit.GetComponent<Enemy>()?.ClearTarget();
        Debug.Log($"[AccordSpiritAgent] Phase 1 — cleansed {hits.Length} enemies");
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator PhaseTwoPortal()
    {
        PlayArrivalCharge();

        // Spawn ring particle — scale ShapeModule.radius to match portal radius
        // Any upgrade to _portalRadius automatically scales the visual
        GameObject ringInstance = null;
        if (_ringParticlePrefab != null)
        {
            ringInstance = Instantiate(_ringParticlePrefab, transform.position,
                                       _ringParticlePrefab.transform.rotation);
            var ringPS = ringInstance.GetComponent<ParticleSystem>();
            if (ringPS != null)
            {
                var shape = ringPS.shape;
                shape.radius = _portalRadius;
                ringPS.Play(true);
            }
        }

        float elapsed = 0f;
        float tickInterval = 0.25f;
        float nextTick = 0f;
        float dpsPerTick = _portalDamagePerSec * tickInterval;

        while (elapsed < _portalDuration)
        {
            if (elapsed >= nextTick)
            {
                Collider[] hits = Physics.OverlapSphere(
                    transform.position, _portalRadius, _enemyLayer);
                foreach (Collider hit in hits)
                    hit.GetComponent<EnemyHealthComponent>()?.TakeDamage(
                        new DamageData(dpsPerTick, DamageType.Ability));
                nextTick += tickInterval;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ringInstance != null) Object.Destroy(ringInstance);
    }

    private void PlayArrivalCharge()
    {
        if (_arrivalChargeEffectPrefab == null) return;
        var go = Instantiate(_arrivalChargeEffectPrefab, transform.position,
                             _arrivalChargeEffectPrefab.transform.rotation, transform);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(true);
            Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax + 0.1f);
        }
        else Destroy(go, 2f);
    }

    private void SeekTarget()
    {
        Vector3 from = _spawnPosition != Vector3.zero
            ? _spawnPosition : transform.position;

        _target = FindNearestUnclaimed(from);

        if (_target != null)
        {
            _claimedTargets?.Add(_target);
            if (!_agent.enabled) _agent.enabled = true;
            _agent.Warp(transform.position);
            _agent.SetDestination(_target.position);
            _spawnPosition = Vector3.zero;
            _seekTimer = 0f;
        }
    }

    private Transform FindNearestUnclaimed(Vector3 from)
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        float best = float.MaxValue;
        Transform result = null;

        foreach (Enemy e in all)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            // Skip targets already claimed by another spirit
            if (_claimedTargets != null && _claimedTargets.Contains(e.transform)) continue;
            float d = Vector3.Distance(from, e.transform.position);
            if (d < best) { best = d; result = e.transform; }
        }

        return result;
    }
}