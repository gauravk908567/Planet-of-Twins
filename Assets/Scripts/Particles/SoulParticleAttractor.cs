using UnityEngine;

/// <summary>
/// Attach to the kill VFX prefab ROOT.
/// Moves individual particles toward the target player each frame using
/// ParticleSystem.GetParticles / SetParticles.
///
/// WHY NOT MoveTowards ON THE TRANSFORM:
///   Particle systems with Simulation Space = World emit particles at world
///   positions. Moving the GameObject has no effect on already-emitted particles.
///   We must manipulate the particle array directly.
///
/// PREFAB SETUP:
///   - Particle System on same GameObject (or child — found via GetComponentInChildren).
///   - Simulation Space: World (particles burst outward first, then we pull them in).
///   - Start Speed: 2-4, Start Lifetime: 2, Loop: OFF, Stop Action: None.
///   - Emission: Rate=0, one Burst at t=0 with Count=12.
///   - KillParticleSpawner calls SetTarget() after Instantiate.
/// </summary>
public class SoulParticleAttractor : MonoBehaviour
{
    [SerializeField] private float attractDelay = 0.5f;   // seconds before pull starts
    [SerializeField] private float attractSpeed = 14f;
    [SerializeField] private float arrivalRadius = 0.4f;   // destroy GO when all close

    private Transform _target;
    private ParticleSystem _ps;
    private ParticleSystem.Particle[] _particles;
    private float _elapsed;
    private bool _hasTarget;

    private void Awake()
    {
        _ps = GetComponentInChildren<ParticleSystem>();
        _particles = new ParticleSystem.Particle[64];
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        _hasTarget = target != null;
        _elapsed = 0f;
    }

    private void LateUpdate()
    {
        if (!_hasTarget || _target == null || _ps == null) return;

        _elapsed += Time.deltaTime;
        if (_elapsed < attractDelay) return;   // let burst expand first

        int count = _ps.GetParticles(_particles);
        if (count == 0) { Destroy(gameObject); return; }

        Vector3 targetPos = _target.position + Vector3.up * 0.8f; // aim at chest
        int alive = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 toTarget = targetPos - _particles[i].position;
            float dist = toTarget.magnitude;

            if (dist <= arrivalRadius)
            {
                // Kill this particle — set remaining lifetime to 0
                _particles[i].remainingLifetime = 0f;
                continue;
            }

            // Move toward target
            _particles[i].velocity = toTarget.normalized * attractSpeed;
            alive++;
        }

        _ps.SetParticles(_particles, count);

        // Once all particles have arrived, destroy the GO
        if (alive == 0)
            Destroy(gameObject);
    }
}