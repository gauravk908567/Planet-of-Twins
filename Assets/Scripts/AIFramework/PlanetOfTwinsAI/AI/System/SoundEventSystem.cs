using UnityEngine;

/// <summary>
/// Global sound event system — enemies hear sounds and investigate.
/// Fire a sound event from any game system. Nearby enemies with
/// PoTPerceptionMemory will receive it and switch to Investigating state.
///
/// ATTACH: To AIManager GO alongside FactionEnergySystem.
///
/// USAGE:
///   SoundEventSystem.Fire(position, radius, SoundType.Footstep);
///   SoundEventSystem.Fire(position, radius, SoundType.ThrowableItem);
/// </summary>
public class SoundEventSystem : MonoBehaviour
{
    public static SoundEventSystem Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    private Vector3 _lastSoundPosition;
    private float _lastSoundRadius;
    private float _gizmoTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_gizmoTimer > 0f) _gizmoTimer -= Time.deltaTime;
    }

    // ── Public API ─────────────────────────────────────────

    public static void Fire(Vector3 position, float radius, SoundType type,
                            float confidenceBoost = 0.5f)
    {
        if (Instance == null) return;
        Instance.ProcessSound(position, radius, type, confidenceBoost);
    }

    private void ProcessSound(Vector3 position, float radius, SoundType type,
                               float confidenceBoost)
    {
        Debug.Log($"[Sound] {type} at {position} radius={radius}");

        // Gizmo debug
        _lastSoundPosition = position;
        _lastSoundRadius = radius;
        _gizmoTimer = 2f;

        // Find all enemies within radius
        var colliders = Physics.OverlapSphere(position, radius);
        foreach (var col in colliders)
        {
            var memory = col.GetComponent<PoTPerceptionMemory>()
                      ?? col.GetComponentInParent<PoTPerceptionMemory>();
            if (memory == null) continue;

            // Only inject if enemy doesn't already have high confidence
            // (actively chasing twin = ignore sounds)
            if (memory.Confidence < 0.8f)
            {
                memory.InjectSoundMemory(position, confidenceBoost);
                MoodEventBus.Fire(EnemySocialEvent.SoundHeard,
                                  new GameObject { name = "SoundSource" });
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || _gizmoTimer <= 0f) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(_lastSoundPosition, _lastSoundRadius);
        Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
        Gizmos.DrawWireSphere(_lastSoundPosition, _lastSoundRadius);
    }
}

public enum SoundType
{
    Footstep,
    ThrowableItem,
    AbilityUse,
    EnemyDeath,
    BarrierPulse,
    PlayerLanding,
}