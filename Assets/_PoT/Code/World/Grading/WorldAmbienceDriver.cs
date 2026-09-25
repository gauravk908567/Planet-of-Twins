using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton — the ONE runtime owner of the world-corruption story dial.
/// A single 0..1 progress value drives, together:
///   • <c>Shader.SetGlobalFloat("_WorldCorruption")</c> — every PoT/Coexistence surface + the
///     Coexistence skybox stain from the same global (their contract: global sits OUTSIDE the
///     material CBUFFER).
///   • <c>RenderSettings.fogColor</c> — distance fog stains from its pure colour toward the
///     corruption tint (same "stain, don't replace" rule).
///   • optionally <see cref="StoryGradeDirector.SetStoryProgress"/> — post grading follows the
///     same dial (off by default; grade beats may want to fire independently).
/// Story code calls <see cref="SetProgress"/> (hard set) or <see cref="TransitionTo"/> (timed,
/// UNSCALED so a cutscene pause never stalls the world falling — R10 comment).
/// R3: lives in Persistent, duplicate-destroy guard, Instance nulled in OnDestroy.
/// </summary>
public class WorldAmbienceDriver : MonoBehaviour
{
    public static WorldAmbienceDriver Instance { get; private set; }

    [Header("Progress (0 = Day of Accord, 1 = fully corrupted)")]
    [SerializeField, Range(0f, 1f)] private float _progress = 0f;
    [Tooltip("Remaps progress → corruption strength (ease the early game, steepen the fall).")]
    [SerializeField] private AnimationCurve _corruptionCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Skybox (Persistent ownership — area scenes need no Lighting setup)")]
    [Tooltip("Assigned to RenderSettings.skybox at boot and after every active-scene change (RenderSettings " +
             "belong to the ACTIVE scene, and SceneFlowManager swaps it while streaming). Empty = leave scenes alone.")]
    [SerializeField] private Material _skyboxMaterial;

    [Header("Distance fog (Unity built-in — per-object, no depth-texture artifacts)")]
    [SerializeField] private bool _driveFog = true;
    [Tooltip("ExponentialSquared density. 0.004 = distant haze, 0.01 = thick. Applied at boot + scene changes.")]
    [SerializeField, Range(0f, 0.05f)] private float _fogDensity = 0.006f;
    [SerializeField] private Color _fogPure = new Color(0.78f, 0.85f, 0.94f);
    [SerializeField] private Color _fogCorrupt = new Color(0.30f, 0.15f, 0.28f);

    [Header("Optional: post grading follows the same dial")]
    [Tooltip("R1 same-scene slot — drag the StoryGradeDirector here. Only used when the toggle is on.")]
    [SerializeField] private StoryGradeDirector _grading;
    [SerializeField] private bool _driveGrading = false;

    private static readonly int WorldCorruptionID = Shader.PropertyToID("_WorldCorruption");
    private Coroutine _transition;   // timers below run on UNSCALED time (R10: world-fall ignores Setsuna/pause)

    public float Progress => _progress;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // R3 duplicate guard
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Instance = null;
            Shader.SetGlobalFloat(WorldCorruptionID, 0f);   // Restart reloads Bootstrap — never leak a stained world
        }
    }

    private void Start()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;   // R8 named handler
        Apply();
    }

    private void OnActiveSceneChanged(Scene previous, Scene next) => Apply();

    /// <summary>Hard-set the dial (checkpoint restore, debug).</summary>
    public void SetProgress(float progress)
    {
        if (_transition != null) { StopCoroutine(_transition); _transition = null; }
        _progress = Mathf.Clamp01(progress);
        Apply();
    }

    /// <summary>Ease the dial to <paramref name="target"/> over <paramref name="seconds"/> (unscaled) —
    /// the "world falls as the story advances" beat.</summary>
    public void TransitionTo(float target, float seconds)
    {
        if (_transition != null) StopCoroutine(_transition);
        _transition = StartCoroutine(TransitionRoutine(Mathf.Clamp01(target), Mathf.Max(0.01f, seconds)));
    }

    private IEnumerator TransitionRoutine(float target, float seconds)
    {
        float from = _progress;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;   // unscaled — see class doc
            _progress = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds));
            Apply();
            yield return null;
        }
        _progress = target;
        Apply();
        _transition = null;
    }

    private void Apply()
    {
        float w = Mathf.Clamp01(_corruptionCurve.Evaluate(_progress));
        Shader.SetGlobalFloat(WorldCorruptionID, w);
        // Play-mode only: an edit-mode OnValidate must never dirty the open scene's Lighting settings.
        if (Application.isPlaying && _skyboxMaterial != null && RenderSettings.skybox != _skyboxMaterial)
            RenderSettings.skybox = _skyboxMaterial;
        if (_driveFog)
        {
            RenderSettings.fogColor = Color.Lerp(_fogPure, _fogCorrupt, w);
            // Play-mode only (same rule as the skybox): edit-mode OnValidate must not dirty scenes.
            if (Application.isPlaying)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = _fogDensity;
            }
        }
        if (_driveGrading && _grading != null) _grading.SetStoryProgress(_progress);
    }

#if UNITY_EDITOR
    // Inspector slider previews live in edit mode too (no runtime state stored anywhere else — R7).
    private void OnValidate() { if (isActiveAndEnabled) Apply(); }
#endif
}
