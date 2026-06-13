using UnityEngine;

/// <summary>
/// Spawns a "soul absorbed" particle at each combat kill position.
/// The particle prefab is responsible for flying to a target � assign a
/// ParticleAttractorTarget component on the prefab and this script sets it.
///
/// HOW TO SET UP THE PARTICLE PREFAB:
///   1. Create a Particle System prefab (name: "SoulAbsorbVFX").
///   2. Set Start Speed > 0, Simulation Space = World.
///   3. Add a sub-emitter or custom script (SoulParticleAttractor) that
///      lerps all particles toward TargetPosition each frame.
///   4. Set Stop Action = Destroy.
///   5. Drag the prefab into this component's particlePrefab slot.
///
/// SoulParticleAttractor script (simple version):
///   public Vector3 TargetPosition;
///   void Update() { transform.position = Vector3.MoveTowards(transform.position, TargetPosition, speed * Time.deltaTime); }
///
/// ARCHITECTURE NOTE: This component subscribes to EnemyDeathNotifier.OnEnemyCombatKill
/// which provides the death world position. It finds the closer of the two twins
/// and sets that as the particle's fly-to target. No scene scan on every kill.
/// </summary>
public class KillParticleSpawner : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private GameObject particlePrefab;

    [Header("Twins (to find nearest target)")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Header("Death notifier")]
    [SerializeField] private EnemyDeathNotifier deathNotifier;

    private void Start()
    {
        deathNotifier ??= EnemyDeathNotifier.Instance;
        if (deathNotifier == null)
        {
            Debug.LogError("[KillParticleSpawner] EnemyDeathNotifier unresolved — is Persistent loaded?", this);
            return;
        }
        // Re-subscribe in case OnEnable fired before deathNotifier was resolved.
        deathNotifier.OnEnemyCombatKill -= HandleCombatKill;
        deathNotifier.OnEnemyCombatKill += HandleCombatKill;
    }

    private void OnEnable()
    {
        if (deathNotifier != null)
            deathNotifier.OnEnemyCombatKill += HandleCombatKill;
    }

    private void OnDisable()
    {
        if (deathNotifier != null)
            deathNotifier.OnEnemyCombatKill -= HandleCombatKill;
    }

    private void HandleCombatKill(Vector3 deathPosition)
    {
        if (particlePrefab == null) return;

        Player nearest = GetNearestTwin(deathPosition);
        if (nearest == null) return;

        GameObject vfx = Instantiate(particlePrefab, deathPosition, Quaternion.identity);

        // Wire target if the prefab has a SoulParticleAttractor component
        var attractor = vfx.GetComponent<SoulParticleAttractor>();
        if (attractor != null)
            attractor.SetTarget(nearest.transform);
    }

    private Player GetNearestTwin(Vector3 pos)
    {
        if (leftTwin == null) return rightTwin;
        if (rightTwin == null) return leftTwin;

        float dLeft = Vector3.Distance(pos, leftTwin.transform.position);
        float dRight = Vector3.Distance(pos, rightTwin.transform.position);
        return dLeft <= dRight ? leftTwin : rightTwin;
    }
}