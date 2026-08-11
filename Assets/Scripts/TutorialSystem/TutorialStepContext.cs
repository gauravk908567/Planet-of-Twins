using UnityEngine;

/// <summary>
/// All scene references a step might need.
/// Passed into every step's Execute() call.
/// Steps read only what they need — ignore the rest.
/// Lives on TutorialDirector GO, wired in Inspector.
/// </summary>
[System.Serializable]
public class TutorialStepContext
{
    [Header("Systems")]
    public TutorialInputGate inputGate;
    public TutorialOverlayController overlay;
    // resetSequencer and failureNotice live in Persistent — resolved by Resolve(), never serialized (R2).
    [System.NonSerialized] public FailureResetSequencer resetSequencer;
    [System.NonSerialized] public FailureNotice failureNotice;

    [Header("Twin control")]
    public MonoBehaviour twinSelectorMono;   // ISelectionLock

    [Header("Checkpoints")]
    public TutorialCheckpointEntry[] checkpoints;

    [Header("Timeline")]
    public UnityEngine.Playables.PlayableDirector timeline;

    [Header("Rescue provider")]
    public MonoBehaviour rescueProviderMono;  // ITutorialRescueProvider

    [Header("Rescue fail reset points")]
    [Tooltip("Where Lyra reappears after a failed rescue attempt.")]
    public Transform rescueFailLeft;
    [Tooltip("Where Kai reappears after a failed rescue attempt — place near the trap.")]
    public Transform rescueFailRight;

    [Header("QTE")]
    public QTESceneAnchor qteAnchor;
    public TutorialHintDisplay hintDisplay;

    [Header("Dev skip ('no tutorial')")]
    [Tooltip("Area-scene roots enabled when the tutorial is SKIPPED (DevConfig.SkipTutorial) — e.g. the main-" +
             "level GO that turns on the area spawn config, normally activated by the intro timeline at cutscene " +
             "end. Same scene as this director, so serializing is R2-safe. Activated with an inactive-ancestor " +
             "guard in SkipTutorial().")]
    public GameObject[] activateOnSkip;

    [Tooltip("Area-scene timeline/cutscene objects DISABLED when the tutorial is skipped — the intro timeline " +
             "director and its dolly-cam / rig roots. Bypassing the cutscene means these must not keep running " +
             "or hold cameras. Same scene (R2-safe). SetActive(false) in SkipTutorial().")]
    public GameObject[] deactivateOnSkip;

    // ── Runtime resolved ──────────────────────────────────────
    [System.NonSerialized] public ISelectionLock SelectionLock;
    [System.NonSerialized] public ITutorialRescueProvider RescueProvider;
    // Persistent (R2 — never serialized cross-scene); resolved by Resolve() like resetSequencer/failureNotice.
    [System.NonSerialized] public FadeController fade;
    [System.NonSerialized] public CameraRotationGuard cameraGuard;

    // ── Rescue reset positions ────────────────────────────────
    public Vector3 RescueFailLeftReset => rescueFailLeft != null
        ? rescueFailLeft.position : Vector3.zero;
    public Vector3 RescueFailRightReset => rescueFailRight != null
        ? rescueFailRight.position : Vector3.zero;

    public void Resolve()
    {
        SelectionLock = twinSelectorMono as ISelectionLock;
        RescueProvider = rescueProviderMono as ITutorialRescueProvider;
        if (overlay == null) overlay = TutorialOverlayController.Instance;
        if (hintDisplay == null) hintDisplay = TutorialHintDisplay.Instance;
        if (SelectionLock == null)
        {
            twinSelectorMono = TwinSelector.Instance;
            SelectionLock = twinSelectorMono as ISelectionLock;
        }
        if (RescueProvider == null)
            RescueProvider = RescueEventController.Instance;

        // inputGate is normally wired in the Inspector (area-scene-local). Fall back to a scene sweep so the dev
        // "no tutorial" skip (which depends on it to open all input) never silently no-ops on an unwired slot.
        if (inputGate == null) inputGate = Object.FindAnyObjectByType<TutorialInputGate>();
        if (inputGate == null)
            Debug.LogError("[TutorialStepContext] No TutorialInputGate found — input stays gated; a tutorial " +
                           "skip cannot open abilities/attack. Wire it on the TutorialDirector.", null);

        resetSequencer = FailureResetSequencer.Instance;
        failureNotice = FailureNotice.Instance;
        if (resetSequencer == null)
            Debug.LogError("[TutorialStepContext] FailureResetSequencer.Instance is null — is Persistent loaded?");
        if (failureNotice == null)
            Debug.LogError("[TutorialStepContext] FailureNotice.Instance is null — is Persistent loaded?");

        // Persistent, non-singleton → FindAnyObjectByType (the allowed scene-scoped sweep, R4 note). Used by the
        // timeline step for the white→game fade-in + camera-flip restore at cutscene end.
        if (fade == null) fade = Object.FindAnyObjectByType<FadeController>();
        if (cameraGuard == null) cameraGuard = Object.FindAnyObjectByType<CameraRotationGuard>();
        if (fade == null)
            Debug.LogWarning("[TutorialStepContext] No FadeController found — cutscene-end fade-in skipped.");
        if (cameraGuard == null)
            Debug.LogWarning("[TutorialStepContext] No CameraRotationGuard — camera-flip restore skipped at cutscene end.");
    }

    /// <summary>Get checkpoint by index. Returns null if out of range.</summary>
    public TutorialCheckpoint GetCheckpoint(int index)
    {
        if (checkpoints == null || index < 0 || index >= checkpoints.Length)
            return null;
        return checkpoints[index].checkpoint;
    }
}