using UnityEngine;

/// <summary>
/// Witness buff aura VFX — two parts:
///
/// 1. PULSE: A periodic particle burst from the Witness outward.
///    Plays every `_pulseInterval` seconds while Witness is alive.
///    Add as child ParticleSystem on Witness prefab.
///
/// 2. BUFF GLOW: A continuous ParticleSystem on each buffed enemy.
///    WitnessEnemy calls NotifyBuffed(enemy)/NotifyUnbuffed(enemy).
///    A looping gold glow PS is spawned on each buffed enemy and destroyed on unbuff.
///
/// PREFAB SETUP:
///   On Witness prefab root — add WitnessAuraVFX component.
///   Wire _auraGlowPrefab: a looping PS (gold particles, loop ON, Play On Awake OFF).
///   Wire _pulseBurst: a child ParticleSystem on Witness (short burst, loop OFF).
///     Duration 0.5s, Burst 20 particles, Shape Sphere 0.3, Speed outward 3-5.
///     Play On Awake OFF — this script controls it.
/// </summary>
public class WitnessAuraVFX : MonoBehaviour
{
    [Header("Witness Pulse Burst (child PS on Witness)")]
    [SerializeField] private ParticleSystem _pulseBurst;
    [SerializeField] private float _pulseInterval = 2.5f;

    [Header("Buff Glow (spawned on each buffed enemy)")]
    [Tooltip("Looping gold glow particle system prefab — spawned as child of buffed enemy")]
    [SerializeField] private GameObject _auraGlowPrefab;

    private float _pulseTimer;
    private System.Collections.Generic.Dictionary<Enemy, GameObject> _activeGlows
        = new System.Collections.Generic.Dictionary<Enemy, GameObject>();

    private void Update()
    {
        if (_pulseBurst == null) return;
        _pulseTimer -= Time.deltaTime;
        if (_pulseTimer <= 0f)
        {
            _pulseBurst.Play();
            _pulseTimer = _pulseInterval;
        }
    }

    /// <summary>Called by WitnessEnemy.UpdateBuffAura when enemy enters aura range.</summary>
    public void NotifyBuffed(Enemy enemy)
    {
        if (_auraGlowPrefab == null || enemy == null) return;
        if (_activeGlows.ContainsKey(enemy)) return;

        var go = Instantiate(_auraGlowPrefab, enemy.transform);
        go.transform.localPosition = Vector3.zero;
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();

        _activeGlows[enemy] = go;
    }

    /// <summary>Called by WitnessEnemy.UpdateBuffAura when enemy leaves aura range.</summary>
    public void NotifyUnbuffed(Enemy enemy)
    {
        if (_activeGlows.TryGetValue(enemy, out var go))
        {
            if (go != null) Destroy(go);
            _activeGlows.Remove(enemy);
        }
    }

    /// <summary>Called on Witness death — clean up all glows.</summary>
    public void StopAll()
    {
        foreach (var kvp in _activeGlows)
            if (kvp.Value != null) Destroy(kvp.Value);
        _activeGlows.Clear();
        if (_pulseBurst != null) _pulseBurst.Stop();
    }
}