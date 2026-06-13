using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Handles both single and dual checkpoint cases.
/// Single — one checkpoint, optional twin requirement.
/// Dual   — two checkpoints simultaneously, each waits for its required twin.
///
/// Wrong twin reset (both modes): one WrongTwinResetHandler instance per step.
/// Identity-based positions — left twin always goes to cpA's left reset point,
/// right twin to cpB's right reset point (cpA if Single).
/// For Dual mode, the handler subscribes to both checkpoints; the shared _resetting
/// guard prevents simultaneous wrong-twin events from racing each other.
///
/// CREATE: Right-click → PlanetOfTwins/Tutorial/Steps/Checkpoint
/// </summary>
[CreateAssetMenu(fileName = "Step_Checkpoint",
                 menuName = "PlanetOfTwins/Tutorial/Steps/Checkpoint")]
public class TutorialCheckpointStepSO : TutorialStepBase
{
    public enum CheckpointMode { Single, Dual }

    [Header("Mode")]
    [Tooltip("Single — one checkpoint. Dual — two checkpoints simultaneously.")]
    public CheckpointMode mode = CheckpointMode.Single;

    [Header("Checkpoint A (used in both modes)")]
    [TutorialCheckpointIndex]
    public int checkpointIndexA = 0;
    [Tooltip("If true, wrong twin triggers failure notice and reset.")]
    public bool requireCorrectTwinA = false;
    public LocalizedString failureMessageA;

    [Header("Checkpoint B (Dual mode only)")]
    [TutorialCheckpointIndex]
    public int checkpointIndexB = 1;

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);

        if (mode == CheckpointMode.Single)
            yield return executor.StartCoroutine(RunSingle(ctx));
        else
            yield return executor.StartCoroutine(RunDual(ctx));
    }

    // ── Single ────────────────────────────────────────────────────────────
    private IEnumerator RunSingle(TutorialStepContext ctx)
    {
        var cp = ctx.GetCheckpoint(checkpointIndexA);
        if (cp == null) yield break;

        bool done = false;
        void OnCorrect(TutorialCheckpoint _) { done = true; }

        WrongTwinResetHandler wrongHandler = requireCorrectTwinA
            ? new WrongTwinResetHandler(ctx, cp, null,
                () => failureMessageA.IsEmpty ? "Switch to the correct twin first"
                                              : failureMessageA.GetLocalizedString())
            : null;

        cp.OnCorrectTwinReached += OnCorrect;
        if (wrongHandler != null) cp.OnWrongTwinReached += wrongHandler.HandleWrongTwin;
        try
        {
            cp.Activate();
            yield return new WaitUntil(() => done);
        }
        finally
        {
            cp.OnCorrectTwinReached -= OnCorrect;
            if (wrongHandler != null) cp.OnWrongTwinReached -= wrongHandler.HandleWrongTwin;
        }
    }

    // ── Dual ──────────────────────────────────────────────────────────────
    private IEnumerator RunDual(TutorialStepContext ctx)
    {
        var cpA = ctx.GetCheckpoint(checkpointIndexA);
        var cpB = ctx.GetCheckpoint(checkpointIndexB);

        if (cpA == null || cpB == null)
        {
            Debug.LogWarning("[TutorialCheckpointStep] Dual mode — one or both checkpoints missing.");
            yield break;
        }

        bool doneA = false;
        bool doneB = false;
        void OnCorrectA(TutorialCheckpoint _) { doneA = true; }
        void OnCorrectB(TutorialCheckpoint _) { doneB = true; }

        // One handler subscribed to both checkpoints — shared _resetting guard prevents races
        var wrongHandler = new WrongTwinResetHandler(ctx, cpA, cpB,
            () => failureMessageA.IsEmpty ? "Wrong twin — switch and try again"
                                          : failureMessageA.GetLocalizedString(),
            () => { doneA = false; doneB = false; });

        cpA.OnCorrectTwinReached += OnCorrectA;
        cpB.OnCorrectTwinReached += OnCorrectB;
        cpA.OnWrongTwinReached += wrongHandler.HandleWrongTwin;
        cpB.OnWrongTwinReached += wrongHandler.HandleWrongTwin;
        try
        {
            cpA.Activate();
            cpB.Activate();
            yield return new WaitUntil(() => doneA && doneB);
        }
        finally
        {
            cpA.OnCorrectTwinReached -= OnCorrectA;
            cpB.OnCorrectTwinReached -= OnCorrectB;
            cpA.OnWrongTwinReached -= wrongHandler.HandleWrongTwin;
            cpB.OnWrongTwinReached -= wrongHandler.HandleWrongTwin;
        }
    }
}
