using System.Collections;
using UnityEngine;

/// <summary>
/// STORY BEAT TRIGGER (repurposed 2026-07-15, generalized same day on user call).
/// One activation-fired checkpoint object that can drive ANY combination of the story dials:
///   • World corruption — <see cref="WorldAmbienceDriver.TransitionTo"/> (skybox / fog /
///     every Coexistence surface stain from the one global).
///   • Post grading — <see cref="StoryGradeDirector.PlayGrade"/> by id
///     (act1_warm / shock / early_fear / mid_purpose / late_chaos / ending_losing).
///   • Sky state — <see cref="SkyStateDriver.BlendTo"/> by id (day / golden_hour / dusk).
/// Fire paths: a Timeline Activation Track turning the GO on (the intro Act 9 pattern — the
/// Persistent "SkyboxChanger" GO is the first instance), plain scene activation, or code
/// calling <see cref="Fire"/>. One shot per lifetime; place one per story beat.
/// NOTE: class name is the legacy one — the Persistent GO + timeline binding reference this
/// script; rename to StoryBeatTrigger only as an isolated GUID-preserving commit (game.md §20).
/// R4: Persistent managers resolved via Instance at fire time, fail-loud.
/// </summary>
public class SkyboxMaterialChange : MonoBehaviour
{
    [Header("Corruption (WorldAmbienceDriver)")]
    [SerializeField] private bool _driveCorruption = true;
    [SerializeField, Range(0f, 1f)] private float _targetProgress = 0.15f;
    [Tooltip("Seconds (UNSCALED) the world takes to ease to the target — slow is the point.")]
    [SerializeField, Min(0.1f)] private float _transitionSeconds = 20f;
    [Tooltip("Only raise the dial — never pull an already-fallen world back (safe on re-activation).")]
    [SerializeField] private bool _onlyIncrease = true;

    [Header("Post grading (StoryGradeDirector)")]
    [Tooltip("Grade id to crossfade to when this beat fires (act1_warm, shock, early_fear, " +
             "mid_purpose, late_chaos, ending_losing). Empty = grading untouched.")]
    [SerializeField] private string _gradeId = "";

    [Header("Sky state (SkyStateDriver)")]
    [Tooltip("Sky state id to blend to when this beat fires (day / golden_hour / dusk). Empty = sky untouched.")]
    [SerializeField] private string _skyStateId = "";
    [Tooltip("Seconds to blend the sky. 0 = instant cut (ApplyState).")]
    [SerializeField, Min(0f)] private float _skyBlendSeconds = 4f;

    private bool _fired;   // one shot per activation lifetime

    private void Start()
    {
        StartCoroutine(FireWhenReady());
    }

    private void OnEnable()
    {
        // Timeline Activation Tracks re-enable without re-running Start — cover both paths.
        StartCoroutine(FireWhenReady());
    }


    private IEnumerator FireWhenReady()
    {
        // Wait one frame so SkyStateDriver (and all other singletons) finish Awake()
        yield return null;
        Fire();
    }
    /// <summary>Fire the beat (idempotent). Also callable directly from story code.</summary>
    public void Fire()
    {
        if (_fired) return;
        bool anyRan = false;

        if (_driveCorruption)
        {
            var driver = WorldAmbienceDriver.Instance;
            if (driver == null)
            {
                Debug.LogError("[StoryBeat] WorldAmbienceDriver not found (Persistent not loaded?) — corruption beat skipped.", this);
            }
            else if (!_onlyIncrease || driver.Progress < _targetProgress)
            {
                driver.TransitionTo(_targetProgress, _transitionSeconds);
                anyRan = true;
            }
            else anyRan = true;   // already past the target — beat is satisfied, don't retry forever
        }

        if (!string.IsNullOrEmpty(_gradeId))
        {
            var grading = StoryGradeDirector.Instance;
            if (grading == null)
                Debug.LogError("[StoryBeat] StoryGradeDirector not found (Persistent not loaded?) — grade beat skipped.", this);
            else
            {
                grading.PlayGrade(_gradeId);
                anyRan = true;
            }
        }

        if (!string.IsNullOrEmpty(_skyStateId))
        {
            var sky = SkyStateDriver.Instance;
            if (sky == null)
                Debug.LogError("[StoryBeat] SkyStateDriver not found (Persistent not loaded?) — sky beat skipped.", this);
            else
            {
                if (_skyBlendSeconds <= 0f) sky.ApplyState(_skyStateId);
                else                        sky.BlendTo(_skyStateId, _skyBlendSeconds);
                anyRan = true;
            }
        }

        // Only latch when at least one dial actually ran (or nothing was configured) — a beat that
        // fired before Persistent existed must stay retryable on the next activation.
        if (anyRan || (!_driveCorruption && string.IsNullOrEmpty(_gradeId) && string.IsNullOrEmpty(_skyStateId)))
            _fired = true;
    }
}
