using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial sequence runner. Genuinely dumb — iterates steps[], nothing else.
///
/// Does NOT auto-start. Call StartTutorial() from a Timeline Signal
/// at the end of the intro cutscene.
///
/// SETUP:
///   1. Wire TutorialStepContext fields in Inspector
///   2. Create step SOs, drag into steps[] in order
///   3. Add Signal Track to Timeline, emit signal at cutscene end
///   4. Add SignalReceiver to TutorialManager GO
///   5. Wire signal → TutorialDirector.StartTutorial()
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [Header("Steps — drag SOs in order")]
    [SerializeField] private TutorialStepBase[] steps;

    [Header("Scene context — all scene refs steps might need")]
    [SerializeField] private TutorialStepContext context;

    private bool _started = false;
    private bool _skipped = false;

    private void Start()
    {
        context.Resolve();

        // Central dev switch — DevConfig.SkipTutorial (master + toggle + build-safe) bypasses the whole tutorial.
        // ownsLevelGos = true: the intro timeline is bypassed entirely, so WE must turn on MainLvl / turn off the
        // timeline rig (the timeline never runs to do it).
        if (DevConfig.SkipTutorial) { SkipTutorial(ownsLevelGos: true); return; }

        context.inputGate?.LockAll();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // Editor-only panic skip — jump straight to gameplay mid-tutorial without redoing it. ownsLevelGos =
        // false: this fires DURING a normal (non-dev) run where the TIMELINE still owns MainLvl / the timeline
        // rig, so we must NOT toggle those GOs — only hand input/cameras to the player.
        // COMBO Ctrl+F9 (bare F9 collided with the profiler binding — same fix class as GameDebuggerV2's
        // Ctrl+`). Raw legacy Input is deliberate here: editor-only dev key, never gameplay (ban precedent).
        if (!_skipped && UnityEngine.Input.GetKey(KeyCode.LeftControl) && UnityEngine.Input.GetKeyDown(KeyCode.F9))
            SkipTutorial(ownsLevelGos: false);
    }
#endif

    /// <summary>Abandon the tutorial and land on the SAME end-state a completed tutorial produces — input fully
    /// open, selection free, stage = Complete. Idempotent.
    /// <para><paramref name="ownsLevelGos"/>: TRUE only for the dev "no tutorial" boot, where the intro timeline
    /// is bypassed entirely — so this method must take over the timeline's job of turning ON the main-level GO
    /// (spawn config) and turning OFF the cutscene rig. FALSE for the editor F9 panic-skip during a NORMAL run,
    /// where the timeline is still alive and owns those GOs — touching them would fight it. Camera/fade cleanup
    /// runs either way.</para></summary>
    public void SkipTutorial(bool ownsLevelGos)
    {
        if (_skipped) return;
        _skipped = true;
        _started = true;                       // block any later StartTutorial() (e.g. a timeline signal)
        StopAllCoroutines();                   // halt a running step sequence

        // ── Canonical "tutorial complete" end-state (mirrors TutorialUnlockAllStepSO) ──
        // Open the gate AND detach it from the reader: detaching is race-proof — a null gate = all input allowed
        // (TwinInputReader), so even if TutorialInputGate.Start() runs AFTER this and re-registers with its
        // default-locked flags, input stays open. AllowAll() also covers the case where the gate stays attached.
        context.inputGate?.AllowAll();
        TwinInputReader.Instance?.SetGate(null);
        TutorialContext.Instance?.SetStage(TutorialStage.Complete);   // flag the game as genuinely past the tutorial

        // Hand the cameras to gameplay mode (off tutorial cams) — the switcher picks the right gameplay
        // cam by distance next frame. FindAnyObjectByType: scene-scoped non-singleton sweep (R4 note).
        var switcher = FindAnyObjectByType<CameraSwitcher>();
        switcher?.SetTutorialMode(false);

        // Skipping bypasses the timeline's end-of-cutscene cleanup, so do it here:
        //  • restore camera rotations (in case a prior run left one flipped),
        //  • CLEAR the fade canvas — otherwise the screen stays opaque (white/black) and you boot into a
        //    blank screen (the "L1_Park loads but screen is white" bug). SetClear = instantly visible, and
        //  • activate the main-level/spawn-config roots the timeline's Activation track would have enabled.
        context.cameraGuard?.RestoreAll();
        context.fade?.SetClear();

        // Only the DEV "no tutorial" skip owns the level/timeline GOs (the timeline is bypassed). The F9 panic-skip
        // during a normal run must NOT touch them — the live timeline is still their owner.
        if (ownsLevelGos)
        {
            DeactivateSkipObjects();   // kill the cutscene/timeline rig FIRST so it can't re-grab cameras…
            ActivateSkipRoots();       // …then turn the main level on.
        }

        Debug.Log($"[TutorialDirector] skip — tutorial bypassed, stage=Complete, input opened, screen cleared" +
                  (ownsLevelGos ? ", timeline off, level activated (dev)." : " (F9 panic — level GOs left to timeline)."));
    }

    /// <summary>Disable the intro timeline / cutscene objects (director + dolly-cam rig). Bypassing the cutscene
    /// means these must not keep playing or hold a camera. A null/missing entry is skipped silently (R5 self-null).</summary>
    private void DeactivateSkipObjects()
    {
        var objs = context?.deactivateOnSkip;
        if (objs == null) return;
        foreach (var go in objs)
            if (go != null) go.SetActive(false);
    }

    /// <summary>Enable the GOs the intro timeline would have activated at cutscene end (e.g. the main-level GO
    /// that turns on the area spawn config). Logs the first inactive ancestor if a root can't actually turn on
    /// (the R11 SetActive-no-op-under-inactive-ancestor trap — same guard as TutorialCheckpoint.Activate()).</summary>
    private void ActivateSkipRoots()
    {
        var roots = context?.activateOnSkip;
        if (roots == null) return;
        foreach (var go in roots)
        {
            if (go == null) continue;
            go.SetActive(true);
            if (go.activeInHierarchy) continue;
            var t = go.transform.parent;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                {
                    Debug.LogError($"[TutorialDirector] activateOnSkip '{go.name}' stayed inactive — ancestor " +
                                   $"'{t.name}' is inactive. Move it out of any Activation Track hierarchy (R11).", go);
                    break;
                }
                t = t.parent;
            }
        }
    }

    /// <summary>
    /// Called by Timeline Signal at end of intro cutscene.
    /// Safe to call multiple times — only starts once.
    /// </summary>
    public void StartTutorial()
    {

        Debug.Log($"[TutorialDirector] StartTutorial called — _started={_started}");
        if (_started) return;
        _started = true;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        yield return new WaitForSecondsRealtime(0.3f);

        foreach (var step in steps)
        {
            if (step == null) continue;
            yield return step.Execute(context, this);
        }
    }
}