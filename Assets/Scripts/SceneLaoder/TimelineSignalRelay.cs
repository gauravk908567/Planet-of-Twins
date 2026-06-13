using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Cross-scene *action* bridge for Timeline Signals (instruction.md §16.2, R11 / BUG-032).
///
/// THE FIX for "a Signal Track needs the target object bound, but the target is cross-scene":
/// put the <c>SignalReceiver</c> on the director's own always-active area-scene GO, point its
/// UnityEvents at the methods below, and let each method forward to the Persistent system at
/// RUNTIME — which crosses scenes freely. Nothing here is ever bound across scenes, and the
/// receiver lives on the director (never deactivated mid-timeline), so the Activation-Track
/// trap (BUG-W15) cannot apply.
///
/// EDITOR WIRING (once per signal):
///   1. Signal Emitter on the track references a SignalAsset (project asset — serializes fine).
///   2. Add a SignalReceiver to THIS director GO.
///   3. Map each SignalAsset → one method below in the SignalReceiver.
///   4. The Signal Track's binding points at this local SignalReceiver (same-scene — serializes).
///
/// APIs verified against code 2026-06-13: FadeController is the existing Persistent fade
/// (on FadeCanvas), a plain MonoBehaviour with NO Instance — resolved here by type.
/// StartFromBlack() reveals the world; StartFromClear() hides it (fades to black).
/// There is no HUDController yet (see HideHUD/ShowHUD notes below).
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class TimelineSignalRelay : MonoBehaviour
{
    private FadeController _fade;

    // R4 — resolve the Persistent fade by type (FadeController has no Instance).
    private void Start() => _fade = FindAnyObjectByType<FadeController>();

    // ── Fade (Persistent FadeController) ─────────────────────────────────────
    /// <summary>Snap to black then fade out to reveal the world — use at cutscene start.</summary>
    public void FadeFromBlack() { if (Resolve()) _fade.StartFromBlack(); }
    /// <summary>Snap to clear then fade in to black — use at cutscene end.</summary>
    public void FadeToBlack()   { if (Resolve()) _fade.StartFromClear(); }

    // ── Tutorial flow ────────────────────────────────────────────────────────
    /// <summary>Start the tutorial sequence (TutorialDirector on this GO).</summary>
    public void StartTutorial()
    {
        var director = GetComponent<TutorialDirector>();
        if (director != null) director.StartTutorial();
        else Debug.LogError("[TimelineSignalRelay] StartTutorial: no TutorialDirector on this GO.", this);
    }

    // ── HUD hide/show ────────────────────────────────────────────────────────
    // No HUDController exists in the project yet. When HUD-hide is actually needed, either
    // (a) add a minimal HUDController singleton on the Persistent HUD canvas, or (b) resolve the
    // Persistent HUD CanvasGroup and toggle alpha/interactable here. Do NOT call a non-existent
    // HUDController.Instance. Left unimplemented on purpose — logs loudly if wired prematurely.
    public void HideHUD() => Debug.LogWarning("[TimelineSignalRelay] HideHUD not implemented — " +
                                              "no HUDController exists yet (instruction.md §16.2).", this);
    public void ShowHUD() => Debug.LogWarning("[TimelineSignalRelay] ShowHUD not implemented — " +
                                              "no HUDController exists yet (instruction.md §16.2).", this);

    // ── Helper ───────────────────────────────────────────────────────────────
    private bool Resolve()
    {
        if (_fade == null) _fade = FindAnyObjectByType<FadeController>();
        if (_fade == null)
            Debug.LogError("[TimelineSignalRelay] No FadeController found — is Persistent loaded?", this);
        return _fade != null;
    }
}
