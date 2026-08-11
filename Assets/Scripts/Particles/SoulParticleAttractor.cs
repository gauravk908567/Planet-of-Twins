using UnityEngine;

/// <summary>
/// Attach to the soul-absorb kill VFX prefab ROOT. Pulls the burst particles toward the nearest
/// twin each frame (ParticleSystem.GetParticles/SetParticles — moving the GameObject can't drag
/// World-space particles).
///
/// POOL-SAFE (Tier 1): this rides a pooled FxManager instance, so it **self-resolves** the
/// target (nearest twin via TwinSelector) and resets on every <c>OnEnable</c> — no external
/// SetTarget, no Destroy (FxManager reclaims the instance by its cue lifetime). The nearest-twin
/// pick happens on the first LateUpdate because OnEnable fires before FxManager positions us.
///
/// PREFAB SETUP: ParticleSystem (self or child), Simulation Space = World, Loop = OFF,
/// Stop Action = None, one burst at t=0.
/// </summary>
public class SoulParticleAttractor : MonoBehaviour
{
    [SerializeField] private float attractDelay = 0.5f;   // seconds before pull starts
    [SerializeField] private float attractSpeed = 14f;
    [SerializeField] private float arrivalRadius = 0.4f;

    private Transform _target;
    private ParticleSystem _ps;
    private ParticleSystem.Particle[] _particles;
    private float _elapsed;
    private bool _hasTarget;
    private bool _needResolve;

    private void Awake()
    {
        _ps = GetComponentInChildren<ParticleSystem>();
        _particles = new ParticleSystem.Particle[64];
    }

    // Reset for pooled reuse. Target is resolved on the first LateUpdate, once FxManager has
    // positioned this instance at the death point (OnEnable fires before that).
    private void OnEnable()
    {
        _elapsed = 0f;
        _hasTarget = false;
        _needResolve = true;
    }

    private void LateUpdate()
    {
        if (_ps == null) return;

        if (_needResolve)
        {
            _target = ResolveNearestTwin();
            _hasTarget = _target != null;
            _needResolve = false;
        }
        if (!_hasTarget || _target == null) return;

        _elapsed += Time.deltaTime;
        if (_elapsed < attractDelay) return;   // let the burst expand first

        int count = _ps.GetParticles(_particles);
        if (count == 0) { _hasTarget = false; return; }   // done — instance reclaimed by cue lifetime

        Vector3 targetPos = _target.position + Vector3.up * 0.8f;   // aim at chest
        int alive = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 toTarget = targetPos - _particles[i].position;
            if (toTarget.magnitude <= arrivalRadius)
            {
                _particles[i].remainingLifetime = 0f;
                continue;
            }
            _particles[i].velocity = toTarget.normalized * attractSpeed;
            alive++;
        }
        _ps.SetParticles(_particles, count);

        if (alive == 0) _hasTarget = false;   // all arrived — stop pulling (no Destroy: pooled)
    }

    private Transform ResolveNearestTwin()
    {
        var sel = TwinSelector.Instance;
        if (sel == null) return null;
        var l = sel.LeftTwin;
        var r = sel.RightTwin;
        if (l == null) return r != null ? r.transform : null;
        if (r == null) return l.transform;
        float dl = (l.transform.position - transform.position).sqrMagnitude;
        float dr = (r.transform.position - transform.position).sqrMagnitude;
        return dl <= dr ? l.transform : r.transform;
    }
}
