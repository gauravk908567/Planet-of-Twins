using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Witness buff aura VFX — fully cue-driven (Banned Lazy Work #10):
///   1. PULSE — a one-shot burst cue played from the Witness every `_pulseInterval` seconds.
///   2. BUFF GLOW — a looping glow cue held per buffed enemy (Follow), stopped on unbuff.
/// No Instantiate / Destroy / pre-placed toggled ParticleSystems. Quick-create the cues from your old
/// pulse + glow prefabs via the Cue Book.
/// </summary>
public class WitnessAuraVFX : MonoBehaviour
{
    [Header("Cue Book — \"pulse\" (one-shot/interval) · \"glow\" (held per enemy)")]
    [SerializeField] private CueBookData _cueBook;
    [SerializeField] private float _pulseInterval = 2.5f;

    private float _pulseTimer;
    private readonly Dictionary<Enemy, CueHandle> _activeGlows = new Dictionary<Enemy, CueHandle>();

    private void Update()
    {
        if (_cueBook == null) return;
        _pulseTimer -= Time.deltaTime;
        if (_pulseTimer <= 0f)
        {
            FxManager.Instance?.PlayBook(_cueBook, "pulse", CueContext.Follow(transform));
            _pulseTimer = _pulseInterval;
        }
    }

    /// <summary>Called by WitnessEnemy.UpdateBuffAura when enemy enters aura range.</summary>
    public void NotifyBuffed(Enemy enemy)
    {
        if (_cueBook == null || enemy == null || _activeGlows.ContainsKey(enemy)) return;
        var handle = FxManager.Instance?.PlayBook(_cueBook, "glow", CueContext.Follow(enemy.transform)) ?? CueHandle.None;
        if (!handle.IsNone) _activeGlows[enemy] = handle;
    }

    /// <summary>Called by WitnessEnemy.UpdateBuffAura when enemy leaves aura range.</summary>
    public void NotifyUnbuffed(Enemy enemy)
    {
        if (enemy != null && _activeGlows.TryGetValue(enemy, out var handle))
        {
            FxManager.Instance?.Stop(handle);
            _activeGlows.Remove(enemy);
        }
    }

    /// <summary>Called on Witness death — clean up all glows.</summary>
    public void StopAll()
    {
        foreach (var kvp in _activeGlows) FxManager.Instance?.Stop(kvp.Value);
        _activeGlows.Clear();
    }
}
