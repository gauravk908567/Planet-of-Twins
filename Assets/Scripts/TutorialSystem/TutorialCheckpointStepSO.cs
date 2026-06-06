using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Handles both single and dual checkpoint cases.
/// Single — one checkpoint, optional twin requirement.
/// Dual   — two checkpoints simultaneously, each waits for its required twin.
///
/// Wrong twin reset in Dual mode:
///   Uses fixed per-checkpoint reset positions rather than swapping.
///   cpA always resets the left twin to cpA's left reset point.
///   cpB always resets the right twin to cpB's right reset point.
///   This ensures correct positioning regardless of which checkpoint fired.
///
/// Shared resetting guard prevents simultaneous wrong-twin events
/// from racing each other.
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
    public LocalizedString failureMessageB;

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
        bool stepComplete = false;

        cp.OnCorrectTwinReached += _ => done = true;

        if (requireCorrectTwinA)
        {
            cp.OnWrongTwinReached += (checkpoint, player) =>
            {
                if (stepComplete) return;

                string msg = failureMessageA.IsEmpty
                    ? "Switch to the correct twin first"
                    : failureMessageA.GetLocalizedString();
                ctx.failureNotice?.Show(msg);

                GetSwappedResets(checkpoint, player,
                    out Vector3 leftReset, out Vector3 rightReset);

                ctx.resetSequencer?.TriggerReset(leftReset, rightReset,
                    () => checkpoint.FullReset());
            };
        }

        cp.Activate();

        yield return new WaitUntil(() => done);
        stepComplete = true;
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
        bool stepComplete = false;
        bool resetting = false;

        cpA.OnCorrectTwinReached += _ => doneA = true;
        cpB.OnCorrectTwinReached += _ => doneB = true;

        cpA.OnWrongTwinReached += (cp, player) =>
        {
            if (stepComplete || resetting) return;
            resetting = true;

            string msg = failureMessageA.IsEmpty
                ? "Wrong twin — switch and try again"
                : failureMessageA.GetLocalizedString();
            ctx.failureNotice?.Show(msg);

            // Always use fixed positions:
            // Left twin (Lyra) → cpA's left reset
            // Right twin (Kai) → cpB's right reset
            GetDualResets(cpA, cpB, out Vector3 leftReset, out Vector3 rightReset);

            ctx.resetSequencer?.TriggerReset(leftReset, rightReset, () =>
            {
                doneA = false;
                doneB = false;
                resetting = false;
                cpA.FullReset();
                cpB.FullReset();
            });
        };

        cpB.OnWrongTwinReached += (cp, player) =>
        {
            if (stepComplete || resetting) return;
            resetting = true;

            string msg = failureMessageB.IsEmpty
                ? "Wrong twin — switch and try again"
                : failureMessageB.GetLocalizedString();
            ctx.failureNotice?.Show(msg);

            // Always use fixed positions:
            // Left twin (Lyra) → cpA's left reset
            // Right twin (Kai) → cpB's right reset
            GetDualResets(cpA, cpB, out Vector3 leftReset, out Vector3 rightReset);

            ctx.resetSequencer?.TriggerReset(leftReset, rightReset, () =>
            {
                doneA = false;
                doneB = false;
                resetting = false;
                cpA.FullReset();
                cpB.FullReset();
            });
        };

        cpA.Activate();
        cpB.Activate();

        yield return new WaitUntil(() => doneA && doneB);
        stepComplete = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Single mode reset — swaps positions based on which twin entered wrong.
    /// </summary>
    private static void GetSwappedResets(TutorialCheckpoint checkpoint, Player wrongPlayer,
        out Vector3 leftReset, out Vector3 rightReset)
    {
        bool wrongIsLeft = wrongPlayer == checkpoint.LeftTwin;

        if (wrongIsLeft)
        {
            leftReset = checkpoint.RightResetPosition;
            rightReset = checkpoint.LeftResetPosition;
        }
        else
        {
            leftReset = checkpoint.LeftResetPosition;
            rightReset = checkpoint.RightResetPosition;
        }
    }

    /// <summary>
    /// Dual mode reset — fixed positions regardless of which checkpoint fired.
    /// Left twin always goes to cpA's left reset (Lyra's correct zone).
    /// Right twin always goes to cpB's right reset (Kai's correct zone).
    /// </summary>
    private static void GetDualResets(
        TutorialCheckpoint cpA, TutorialCheckpoint cpB,
        out Vector3 leftReset, out Vector3 rightReset)
    {
        leftReset = cpA.LeftResetPosition;
        rightReset = cpB.RightResetPosition;
    }
}