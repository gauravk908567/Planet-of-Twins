using UnityEngine;

/// <summary>
/// RainController — Persistent singleton, the single owner of the rain state (2026-07-17).
/// One toggle + one 0..1 intensity (drizzle → heavy). Everything scales off intensity:
///   • falling-drop / splash / ripple particle emission (serialized systems, camera-following)
///   • the _PoTWetness shader global (Coexistence darken+glint, terrain darken+gloss,
///     detail-grass darken) — wetness LAGS intensity: ground soaks and dries over seconds
///   • the rain audio loop volume (serialized looping AudioSource, 2D)
/// Registered area drip emitters (eaves) follow the same intensity via RainDripLine (R5:
/// area objects self-register; this manager null-purges before iterating).
///
/// Time rules (R10): particles run on SCALED time (slow in Setsuna, frozen in pause —
/// correct, rain is world-sim); the soak/dry lag uses Time.deltaTime (scaled) for the
/// same reason. No Time.timeScale writes here.
/// Editor safety: _PoTWetness is a GLOBAL — zeroed in OnDestroy so play mode never
/// leaks a soaked world into the editor (GraphicsSettingsController pattern).
/// Camera follow: lazy Camera.main re-resolve every frame (UIBillboard lazy-fix pattern —
/// a one-shot Start() lookup dies on boot order).
/// </summary>
public class RainController : MonoBehaviour
{
    public static RainController Instance { get; private set; }

    [Header("State (drive these — or call SetRain)")]
    [SerializeField] private bool _raining = false;
    [Range(0f, 1f)]
    [Tooltip("0.15 ≈ light drizzle, 1 = downpour. Everything scales off this.")]
    [SerializeField] private float _intensity = 0.5f;

    [Header("Particles (children of this object)")]
    [Tooltip("Falling drops — camera-local box emitter; emission = rate × intensity.")]
    [SerializeField] private ParticleSystem _drops;
    [Tooltip("Ground ripple rings (sub-emitter systems may be left null — drops' own sub-emitters then own them).")]
    [SerializeField] private ParticleSystem _ripples;
    [Tooltip("Impact splash sprays (optional, may be null).")]
    [SerializeField] private ParticleSystem _splashes;
    [SerializeField] private float _dropsMaxRate = 900f;
    [SerializeField] private float _ripplesMaxRate = 220f;
    [SerializeField] private float _splashesMaxRate = 120f;

    [Header("Follow")]
    [Tooltip("Rain volume rides the camera on XZ (and Y + this offset) so it never runs out.")]
    [SerializeField] private float _heightAboveCamera = 12f;

    [Header("Wetness (global _PoTWetness)")]
    [Tooltip("Seconds of full rain for the ground to fully soak. Scaled time.")]
    [SerializeField] private float _soakSeconds = 12f;
    [Tooltip("Seconds for a soaked ground to fully dry after rain stops. Scaled time.")]
    [SerializeField] private float _drySeconds = 45f;

    [Header("Audio")]
    [Tooltip("Looping 2D rain bed on a child AudioSource; volume = curve(intensity). May be null until authored.")]
    [SerializeField] private AudioSource _rainLoop;
    [SerializeField] private float _rainLoopMaxVolume = 0.65f;

    private static readonly int WetnessID = Shader.PropertyToID("_PoTWetness");

    private float _wetness;              // lagged 0..1, written to the shader global
    private Transform _cameraTransform;  // lazy re-resolve — never cache once in Start
    private float _appliedIntensity = -1f;
    private bool _appliedRaining;

    /// <summary>Current public state (reads for UI/debug).</summary>
    public bool IsRaining => _raining;
    public float Intensity => _intensity;
    public float Wetness => _wetness;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Fail loud on the one required slot; ripples/splash/audio are optional layers.
        if (_drops == null)
            Debug.LogError("[RainController] Drops particle system not assigned — rain has no body.", this);
        ApplyState(force: true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Shader.SetGlobalFloat(WetnessID, 0f);   // never leak a soaked world past play mode
    }

    /// <summary>Single entry point for gameplay/story/debug: turn rain on/off at an intensity.</summary>
    public void SetRain(bool raining, float intensity)
    {
        _raining = raining;
        _intensity = Mathf.Clamp01(intensity);
    }

    private void Update()
    {
        // Follow the gameplay camera (lazy re-resolve; Persistent boots before area cams).
        if (_cameraTransform == null)
        {
            var main = Camera.main;
            if (main != null) _cameraTransform = main.transform;
        }
        if (_cameraTransform != null)
        {
            Vector3 p = _cameraTransform.position;
            transform.position = new Vector3(p.x, p.y + _heightAboveCamera, p.z);
        }

        ApplyState(force: false);

        // Wetness lags: soak while raining, dry when not. Scaled time — world-sim (R10).
        float target = _raining ? _intensity : 0f;
        float rate = _raining ? (1f / Mathf.Max(0.01f, _soakSeconds))
                              : (1f / Mathf.Max(0.01f, _drySeconds));
        _wetness = Mathf.MoveTowards(_wetness, target, rate * Time.deltaTime);
        Shader.SetGlobalFloat(WetnessID, _wetness);
    }

    private void ApplyState(bool force)
    {
        if (!force && _appliedRaining == _raining && Mathf.Approximately(_appliedIntensity, _intensity))
            return;
        _appliedRaining = _raining;
        _appliedIntensity = _intensity;

        float k = _raining ? _intensity : 0f;
        SetRate(_drops, _dropsMaxRate * k);
        SetRate(_ripples, _ripplesMaxRate * k);
        SetRate(_splashes, _splashesMaxRate * k);

        if (_rainLoop != null)
        {
            // sqrt curve: drizzle is already audible, heavy doesn't blow out
            _rainLoop.volume = Mathf.Sqrt(k) * _rainLoopMaxVolume;
            if (_raining && !_rainLoop.isPlaying) _rainLoop.Play();
            else if (!_raining && _rainLoop.isPlaying) _rainLoop.Stop();
        }
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        if (rate > 0f && !ps.isPlaying) ps.Play();
        else if (rate <= 0f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

#if UNITY_EDITOR
    [ContextMenu("TEST: Drizzle (0.15)")] private void TestDrizzle() => SetRain(true, 0.15f);
    [ContextMenu("TEST: Heavy (1.0)")] private void TestHeavy() => SetRain(true, 1f);
    [ContextMenu("TEST: Stop rain")] private void TestStop() => SetRain(false, _intensity);
#endif
}
