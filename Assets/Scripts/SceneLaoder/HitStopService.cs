using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent singleton — micro time-freeze on impact ("hit-stop", the Platinum/Capcom contact
/// feel). A new OWNER of <see cref="TimeScaleService"/> (R10): Punch() = Request(this, scale),
/// released after an UNSCALED wait, min-value-wins alongside Setsuna/pause/soul-travel.
/// Generic by contract (rule 11): ANY impact path may call Punch — melee contact is the first
/// wired consumer; ability hits, kills and finishers pass their own strength/duration.
/// Overlapping punches extend the window (latest end wins) — a flurry never stutters the release.
/// </summary>
public class HitStopService : MonoBehaviour
{
    public static HitStopService Instance { get; private set; }

    [Header("Default punch (melee contact)")]
    [Tooltip("Time scale during the stop. 0.05 = near-freeze; 0.3 = soft emphasis.")]
    [SerializeField, Range(0.01f, 0.5f)] private float _defaultScale = 0.05f;
    [Tooltip("UNSCALED seconds the stop lasts. 0.04–0.08 melee, up to ~0.15 for heavy finishers.")]
    [SerializeField, Range(0.02f, 0.3f)] private float _defaultDuration = 0.06f;

    private float _releaseAt;         // unscaled time — R10: the stop must outlive its own freeze
    private Coroutine _running;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // R3 duplicate guard
        Instance = this;
        FxManager.HitStopHook = Punch;   // P19 seam 3 — cue-book elements punch through this hook
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            TimeScaleService.Instance?.Release(this);   // never strand a frozen world across Restart
            FxManager.HitStopHook = null;               // stale static across Restart = the canary bug
            Instance = null;
        }
    }

    /// <summary>Default melee-contact punch.</summary>
    public void Punch() => Punch(_defaultScale, _defaultDuration);

    /// <summary>Custom punch — heavier hits pass a lower scale / longer duration.</summary>
    public void Punch(float timeScale, float unscaledDuration)
    {
        var svc = TimeScaleService.Instance;
        if (svc == null) return;                        // no service = no feel, never a crash
        _releaseAt = Mathf.Max(_releaseAt, Time.unscaledTime + Mathf.Max(0.01f, unscaledDuration));
        svc.Request(this, Mathf.Clamp(timeScale, 0.01f, 1f));
        if (_running == null) _running = StartCoroutine(ReleaseWhenDue());
    }

    private IEnumerator ReleaseWhenDue()
    {
        while (Time.unscaledTime < _releaseAt) yield return null;   // unscaled — we froze scaled time
        TimeScaleService.Instance?.Release(this);
        _running = null;
    }
}
