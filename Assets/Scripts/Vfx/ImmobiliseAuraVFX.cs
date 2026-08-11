using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared per-enemy "immobilise aura" looping VFX — a held cue ("loop") played on each affected enemy
/// (Follow) and stopped when the effect ends. Driven by **Possess** (and Coalesce, if wired). **Stun no
/// longer uses this** — StunAbility owns its own `OnStun_Hit` per stunned enemy ( / instruction.md
/// ). Renamed from `StunVFXSystem` so a class named for one ability no longer quietly serves several
/// (Banned Lazy Work #14). Pooled-enemy reuse is safe via handle staleness; despawn is covered by
/// EnemyPool → FxManager.StopAllOn.
///
/// SETUP:
///   - Place on any always-active scene GameObject (e.g. GameSystem).
///   - Drag TwinAbilitySetup into _abilitySetup.
///   - Assign a Cue Book whose "loop" effect is the looping aura ParticleSystem (element attachMode Follow +
///     head-height localOffset.y).
/// </summary>
public class ImmobiliseAuraVFX : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private TwinAbilitySetup _abilitySetup;

    [Header("Cue")]
    [Tooltip("Cue Book — plays the held \"loop\" aura on each affected enemy (Follow). The element's prefab is " +
             "the looping ParticleSystem; set the element's attachMode Follow + head-height localOffset.y.")]
    [SerializeField] private CueBookData _cueBook;
    [Tooltip("Optional explicit FxManager (R1). Resolves to .Instance otherwise (R4).")]
    [SerializeField] private FxManager _fx;

    private IPossessEvents _possessEvents;

    // Held cue handle per affected enemy. Handle staleness (version-stamped) makes pooled-enemy reuse safe.
    private readonly Dictionary<GameObject, CueHandle> _activeVFX = new Dictionary<GameObject, CueHandle>();
    private readonly List<GameObject> _stale = new List<GameObject>();

    private void Start() => StartCoroutine(LateStart());

    private System.Collections.IEnumerator LateStart()
    {
        yield return null; // wait for TwinAbilitySetup.Start()
        _possessEvents = _abilitySetup?.PossessEvents;
        if (_possessEvents == null)
            Debug.LogError("[ImmobiliseAuraVFX] PossessEvents null — check TwinAbilitySetup.", this);
        Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_possessEvents != null)
        {
            _possessEvents.OnPossessApplied += HandleApplied;
            _possessEvents.OnPossessEnded += HandleEnded;
        }
    }

    private void Unsubscribe()
    {
        if (_possessEvents != null)
        {
            _possessEvents.OnPossessApplied -= HandleApplied;
            _possessEvents.OnPossessEnded -= HandleEnded;
        }
    }

    private void HandleApplied(GameObject enemy)
    {
        if (enemy == null || _cueBook == null) return;
        _fx ??= FxManager.Instance;   // R4
        if (_fx == null) return;

        if (_activeVFX.TryGetValue(enemy, out var existing))
        {
            if (_fx.IsPlaying(existing)) return;   // genuinely still showing — don't double spawn
            _activeVFX.Remove(enemy);              // stale (pool reclaimed it) → respawn cleanly
        }

        // Held aura element until Stop() (FxManager reads main.loop). No Instantiate, no parenting, no Destroy.
        var handle = _fx.PlayBook(_cueBook, "loop", CueContext.Follow(enemy.transform));
        if (!handle.IsNone) _activeVFX[enemy] = handle;
    }

    private void HandleEnded(GameObject enemy)
    {
        if (enemy == null) return;
        if (!_activeVFX.TryGetValue(enemy, out var handle)) return;
        _activeVFX.Remove(enemy);
        _fx?.Stop(handle);   // graceful stop + pool reclaim (stale handle = no-op)
    }

    // Clean up entries whose enemy was destroyed (non-pooled). Pooled despawns are covered by
    // EnemyPool → StopAllOn + the staleness check in HandleApplied.
    private void LateUpdate()
    {
        if (_activeVFX.Count == 0) return;
        _stale.Clear();
        foreach (var kvp in _activeVFX)
            if (kvp.Key == null) _stale.Add(kvp.Key);
        foreach (var k in _stale)
        {
            if (_activeVFX.TryGetValue(k, out var h)) _fx?.Stop(h);
            _activeVFX.Remove(k);
        }
    }
}
