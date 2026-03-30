using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and manages a looping particle system on enemies affected by
/// Stun, Possess, or Coalesce. All three share the same visual.
///
/// SETUP:
///   - Place on any always-active scene GameObject (e.g. GameSystem).
///   - Drag TwinAbilitySetup into _abilitySetup slot.
///   - Drag your stun particle system prefab into _stunVFXPrefab slot.
///     Prefab settings: Play On Awake = ON, Loop = ON, Stop Action = None.
/// </summary>
public class StunVFXSystem : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private TwinAbilitySetup _abilitySetup;

    [Header("Prefab")]
    [SerializeField] private GameObject _stunVFXPrefab;

    [Tooltip("Y offset from enemy root to place VFX at head height. " +
             "Adjust per enemy type if needed.")]
    [SerializeField] private float _vfxHeightOffset = 0.8f;

    private IStunEvents _stunEvents;
    private IPossessEvents _possessEvents;

    // One VFX instance per affected enemy — keyed by enemy GameObject
    private readonly Dictionary<GameObject, ParticleSystem> _activeVFX
        = new Dictionary<GameObject, ParticleSystem>();

    private void Start() => StartCoroutine(LateStart());

    private System.Collections.IEnumerator LateStart()
    {
        yield return null; // wait for TwinAbilitySetup.Start()

        _stunEvents = _abilitySetup?.StunEvents;
        _possessEvents = _abilitySetup?.PossessEvents;

        if (_stunEvents == null)
            Debug.LogError("[StunVFXSystem] StunEvents null — check TwinAbilitySetup.", this);
        if (_possessEvents == null)
            Debug.LogError("[StunVFXSystem] PossessEvents null — check TwinAbilitySetup.", this);

        Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_stunEvents != null)
        {
            _stunEvents.OnStunApplied += HandleApplied;
            _stunEvents.OnStunEnded += HandleEnded;
        }
        if (_possessEvents != null)
        {
            _possessEvents.OnPossessApplied += HandleApplied;
            _possessEvents.OnPossessEnded += HandleEnded;
        }
    }

    private void Unsubscribe()
    {
        if (_stunEvents != null)
        {
            _stunEvents.OnStunApplied -= HandleApplied;
            _stunEvents.OnStunEnded -= HandleEnded;
        }
        if (_possessEvents != null)
        {
            _possessEvents.OnPossessApplied -= HandleApplied;
            _possessEvents.OnPossessEnded -= HandleEnded;
        }
    }

    private void HandleApplied(GameObject enemy)
    {
        if (enemy == null || _stunVFXPrefab == null) return;

        // Already has VFX — don't double spawn
        if (_activeVFX.TryGetValue(enemy, out var existing) && existing != null)
            return;

        // Instantiate parented to enemy so it follows movement.
        // Use the prefab's own rotation (X=-90 baked in to orient the circle upward).
        // Spawn at enemy root + height offset so it sits at head level.
        Vector3 spawnPos = enemy.transform.position + Vector3.up * _vfxHeightOffset;
        var go = Instantiate(_stunVFXPrefab, spawnPos,
                             _stunVFXPrefab.transform.rotation, enemy.transform);
        var ps = go.GetComponent<ParticleSystem>();

        if (ps == null)
        {
            Debug.LogWarning("[StunVFXSystem] Prefab has no ParticleSystem component.", this);
            Destroy(go);
            return;
        }

        _activeVFX[enemy] = ps;
    }

    private void HandleEnded(GameObject enemy)
    {
        if (enemy == null) return;
        if (!_activeVFX.TryGetValue(enemy, out var ps)) return;

        _activeVFX.Remove(enemy);

        if (ps == null) return;

        // Stop emitting and let existing particles fade naturally,
        // then destroy the GameObject after the particle lifetime expires
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(ps.gameObject, ps.main.startLifetime.constantMax + 0.5f);
    }

    // Clean up stale entries if enemy is destroyed while effect is active
    private void LateUpdate()
    {
        var stale = new System.Collections.Generic.List<GameObject>();
        foreach (var kvp in _activeVFX)
            if (kvp.Key == null || kvp.Value == null) stale.Add(kvp.Key);
        foreach (var k in stale) _activeVFX.Remove(k);
    }
}