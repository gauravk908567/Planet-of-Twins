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

    // ── Runtime resolved ──────────────────────────────────────
    [System.NonSerialized] public ISelectionLock SelectionLock;
    [System.NonSerialized] public ITutorialRescueProvider RescueProvider;

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

        resetSequencer = FailureResetSequencer.Instance;
        failureNotice = FailureNotice.Instance;
        if (resetSequencer == null)
            Debug.LogError("[TutorialStepContext] FailureResetSequencer.Instance is null — is Persistent loaded?");
        if (failureNotice == null)
            Debug.LogError("[TutorialStepContext] FailureNotice.Instance is null — is Persistent loaded?");
    }

    /// <summary>Get checkpoint by index. Returns null if out of range.</summary>
    public TutorialCheckpoint GetCheckpoint(int index)
    {
        if (checkpoints == null || index < 0 || index >= checkpoints.Length)
            return null;
        return checkpoints[index].checkpoint;
    }
}