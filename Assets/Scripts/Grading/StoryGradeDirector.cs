using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// StoryGradeDirector (Persistent, P17).
/// Owns the single global StoryGradeVolume (priority 0) and crossfades its
/// VolumeProfile across the 6 story-grade profiles as the story progresses.
///
/// Mechanism: two global Volumes (A/B) on child GOs, both priority 0. The active
/// one holds the current profile at weight 1; a blend loads the target profile
/// into the inactive volume and lerps weights over _blendSeconds (UNSCALED time —
/// grading must move during Setsuna/pause). Rows with hardCut (Shock) snap.
///
/// Progression source: CheckpointManager carries no story flags yet, so the
/// director exposes the generic seam instead of guessing one:
///   SetStoryProgress(0..1)  — picks the window row (highest minProgress ≤ p)
///   PlayGrade(id)           — event-driven grades (e.g. "shock", hard cut);
///                             the next SetStoryProgress returns to the arc.
/// Beat→progress mapping lives with the caller (checkpoint/story data), never here.
/// </summary>
public class StoryGradeDirector : MonoBehaviour
{
    public static StoryGradeDirector Instance { get; private set; }

    [System.Serializable]
    public class GradeRow
    {
        [Tooltip("Stable id, e.g. act1_warm / shock / early_fear …")]
        public string id;
        public VolumeProfile profile;
        [Tooltip("Story progress (0..1) at which this grade takes over. Negative = never auto-selected (event-only rows like Shock).")]
        public float minProgress;
        [Tooltip("Snap instead of crossfade (Shock).")]
        public bool hardCut;
    }

    [Header("Volumes (children, both global, priority 0)")]
    [SerializeField] private Volume _volumeA;
    [SerializeField] private Volume _volumeB;

    [Header("Grades (sorted by minProgress; event-only rows use minProgress < 0)")]
    [SerializeField] private GradeRow[] _grades;

    [Tooltip("Crossfade length in seconds — UNSCALED time.")]
    [SerializeField] private float _blendSeconds = 4f;

    private Volume _active;          // currently at weight 1 (or blending up)
    private Volume _inactive;
    private GradeRow _current;
    private Coroutine _blend;        // unscaled-time crossfade
    private float _progress;

    public float StoryProgress => _progress;
    public string CurrentGradeId => _current != null ? _current.id : null;

    // ── Lifecycle (R8: Awake wires self) ─────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_volumeA == null || _volumeB == null || _grades == null || _grades.Length == 0)
        {
            Debug.LogError("[StoryGradeDirector] Volumes or grade rows unassigned — grading disabled.", this);
            enabled = false;
            return;
        }

        _active = _volumeA;
        _inactive = _volumeB;
        _active.weight = 1f;
        _inactive.weight = 0f;
    }

    private void Start()
    {
        if (!enabled) return;
        // Boot into the progress-0 grade immediately (no fade on first frame).
        var row = PickRow(0f);
        if (row == null || row.profile == null)
        {
            Debug.LogError("[StoryGradeDirector] No grade row covers progress 0 (need one with minProgress == 0) — grading disabled.", this);
            enabled = false;
            return;
        }
        _current = row;
        _active.profile = row.profile;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Public API ────────────────────────────────────────────────────────────
    /// <summary>All authored grade ids, in row order (debug/tooling — the GameDebuggerV2 grade bench).</summary>
    public string[] GradeIds()
    {
        var ids = new string[_grades != null ? _grades.Length : 0];
        for (int i = 0; i < ids.Length; i++) ids[i] = _grades[i] != null ? _grades[i].id : "";
        return ids;
    }

    /// <summary>Story progress 0..1 — selects the window grade.</summary>
    public void SetStoryProgress(float progress)
    {
        if (!enabled) return;
        _progress = Mathf.Clamp01(progress);
        var row = PickRow(_progress);
        if (row == null || row == _current) return;
        Apply(row);
    }

    /// <summary>Event-driven grade by id (e.g. "shock" — hard cut). Unknown id = loud no-op.</summary>
    public void PlayGrade(string id)
    {
        if (!enabled) return;
        for (int i = 0; i < _grades.Length; i++)
        {
            if (_grades[i] != null && _grades[i].id == id)
            {
                if (_grades[i] != _current) Apply(_grades[i]);
                return;
            }
        }
        Debug.LogError($"[StoryGradeDirector] PlayGrade: no grade row with id '{id}'.", this);
    }

    // ── Internals ─────────────────────────────────────────────────────────────
    private GradeRow PickRow(float p)
    {
        GradeRow best = null;
        for (int i = 0; i < _grades.Length; i++)
        {
            var g = _grades[i];
            if (g == null || g.profile == null || g.minProgress < 0f) continue; // event-only rows never auto-pick
            if (g.minProgress <= p && (best == null || g.minProgress > best.minProgress))
                best = g;
        }
        return best;
    }

    private void Apply(GradeRow row)
    {
        if (row.profile == null)
        {
            Debug.LogError($"[StoryGradeDirector] Grade '{row.id}' has no profile assigned.", this);
            return;
        }
        _current = row;

        if (_blend != null) { StopCoroutine(_blend); _blend = null; }

        if (row.hardCut)
        {
            _active.profile = row.profile;
            _active.weight = 1f;
            _inactive.weight = 0f;
            return;
        }

        _inactive.profile = row.profile;
        _blend = StartCoroutine(Crossfade());
    }

    private IEnumerator Crossfade()
    {
        float elapsed = 0f; // unscaled — grading keeps moving during Setsuna/pause
        while (elapsed < _blendSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _blendSeconds);
            _active.weight = 1f - t;
            _inactive.weight = t;
            yield return null;
        }
        // Swap roles: the target volume is now the active one.
        var was = _active;
        _active = _inactive;
        _inactive = was;
        _active.weight = 1f;
        _inactive.weight = 0f;
        _blend = null;
    }
}
