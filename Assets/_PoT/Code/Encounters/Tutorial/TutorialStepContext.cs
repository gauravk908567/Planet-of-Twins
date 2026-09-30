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
        RescueProvider = rescueProviderMono as ITutorialRescueProvider;
        if (overlay == null) overlay = TutorialOverlayController.Instance;
        if (hintDisplay == null) hintDisplay = TutorialHintDisplay.Instance;
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

    /// <summary>
    /// The checkpoint whose entry carries <paramref name="id"/> (the hidden stable id step SOs store). Null + LogError
    /// when no entry has it (the entry was removed) or several do.
    /// </summary>
    public TutorialCheckpoint GetCheckpoint(string id)
    {
        TutorialCheckpoint found = null;
        int matches = 0;
        if (checkpoints != null && !string.IsNullOrEmpty(id))
        {
            foreach (var entry in checkpoints)
            {
                if (entry == null || entry.id != id) continue;
                if (matches++ == 0) found = entry.checkpoint;
            }
        }

        if (matches == 1) return found;
        Debug.LogError(matches == 0
            ? $"[TutorialStepContext] No checkpoint entry has id '{id}' — the entry was removed, or the step was " +
              "never picked. Re-pick it on the step SO (open L1_Park)."
            : $"[TutorialStepContext] {matches} checkpoint entries share id '{id}' — save L1_Park once so " +
              "TutorialDirector re-issues the copies' ids.");
        return null;
    }

    /// <summary>
    /// Gives every checkpoint entry a unique stable id. A new entry, or one duplicated in the Inspector (Unity copies
    /// the id with it), gets a fresh one; the first holder of an id keeps it. True when an id was assigned.
    /// Editor authoring only (TutorialDirector.OnValidate).
    /// </summary>
    public bool EnsureCheckpointIds()
    {
        if (checkpoints == null) return false;
        bool changed = false;
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (var entry in checkpoints)
        {
            if (entry == null) continue;
            if (string.IsNullOrEmpty(entry.id) || !seen.Add(entry.id))
            {
                entry.id = System.Guid.NewGuid().ToString("N");
                seen.Add(entry.id);
                changed = true;
            }
        }
        return changed;
    }
}