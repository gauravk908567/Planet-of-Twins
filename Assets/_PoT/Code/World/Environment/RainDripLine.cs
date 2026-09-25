using UnityEngine;

/// <summary>
/// RainDripLine — area-resident drip emitter for eaves / awnings / rope lines (2026-07-17).
/// Place one under a roof edge with a ParticleSystem child (edge/box shape along the eave,
/// slow falling drips). It self-registers with RainController-style intensity by polling the
/// singleton in Update — no serialized cross-scene ref (R2/R4): optional same-scene slot
/// pattern doesn't apply since RainController is Persistent, so this resolves via
/// Instance and fails OPEN (no rain manager = no drips, no errors on direct-area play).
///
/// Drips lag the rain slightly (roofs keep dripping after a shower stops) via _lagSeconds.
/// Timers are SCALED time — world-sim, matches RainController (R10).
/// </summary>
public class RainDripLine : MonoBehaviour
{
    [Tooltip("Drip particle system (child). Emission = max rate × lagged rain intensity.")]
    [SerializeField] private ParticleSystem _drips;
    [SerializeField] private float _maxRate = 14f;
    [Tooltip("Seconds the drips take to follow the rain up/down (roofs drip after rain stops).")]
    [SerializeField] private float _lagSeconds = 6f;

    private float _lagged;   // scaled-time smoothed intensity
    private float _applied = -1f;

    private void Awake()
    {
        if (_drips == null) _drips = GetComponentInChildren<ParticleSystem>();
        if (_drips == null)
        {
            Debug.LogError("[RainDripLine] No ParticleSystem found — disabling.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        var rain = RainController.Instance;                       // fail open when absent
        float target = (rain != null && rain.IsRaining) ? rain.Intensity : 0f;
        _lagged = Mathf.MoveTowards(_lagged, target, Time.deltaTime / Mathf.Max(0.01f, _lagSeconds));

        if (Mathf.Approximately(_applied, _lagged)) return;
        _applied = _lagged;

        var emission = _drips.emission;
        emission.rateOverTime = _maxRate * _lagged;
        if (_lagged > 0.001f && !_drips.isPlaying) _drips.Play();
        else if (_lagged <= 0.001f && _drips.isPlaying) _drips.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }
}
