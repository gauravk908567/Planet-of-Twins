using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Handles both single and dual checkpoint cases.
/// Single — one checkpoint, optional twin requirement.
/// Dual   — two checkpoints simultaneously, each waits for its required twin.
///
/// Wrong twin reset — swaps reset positions so each twin lands at their own
/// correct reset point regardless of which checkpoint was entered incorrectly.
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

                // Swap reset positions so each twin lands at their correct spot
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

        cpA.OnCorrectTwinReached += _ => doneA = true;
        cpB.OnCorrectTwinReached += _ => doneB = true;

        cpA.OnWrongTwinReached += (cp, player) =>
        {
            if (stepComplete) return;

            string msg = failureMessageA.IsEmpty
                ? "Wrong twin — switch and try again"
                : failureMessageA.GetLocalizedString();
            ctx.failureNotice?.Show(msg);

            GetSwappedResets(cp, player, out Vector3 leftReset, out Vector3 rightReset);

            ctx.resetSequencer?.TriggerReset(leftReset, rightReset, () =>
            {
                doneA = false;
                doneB = false;
                cpA.FullReset();
                cpB.FullReset();
            });
        };

        cpB.OnWrongTwinReached += (cp, player) =>
        {
            if (stepComplete) return;

            string msg = failureMessageB.IsEmpty
                ? "Wrong twin — switch and try again"
                : failureMessageB.GetLocalizedString();
            ctx.failureNotice?.Show(msg);

            GetSwappedResets(cp, player, out Vector3 leftReset, out Vector3 rightReset);

            ctx.resetSequencer?.TriggerReset(leftReset, rightReset, () =>
            {
                doneA = false;
                doneB = false;
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
    /// Determines correct reset positions for both twins based on which twin
    /// entered the wrong checkpoint.
    ///
    /// If the wrong twin is the LEFT twin:
    ///   Left twin should go to the RIGHT reset point (where they should be)
    ///   Right twin should go to the LEFT reset point (where they should be)
    /// And vice versa.
    ///
    /// This ensures each twin is placed at the reset point that belongs to them,
    /// not both twins at the wrong checkpoint's default positions.
    /// </summary>
    private static void GetSwappedResets(TutorialCheckpoint checkpoint, Player wrongPlayer,
        out Vector3 leftReset, out Vector3 rightReset)
    {
        bool wrongIsLeft = wrongPlayer == checkpoint.LeftTwin;

        if (wrongIsLeft)
        {
            // Left twin entered wrong — swap so left goes to right reset position
            leftReset = checkpoint.RightResetPosition;
            rightReset = checkpoint.LeftResetPosition;
        }
        else
        {
            // Right twin entered wrong — swap so right goes to left reset position
            leftReset = checkpoint.LeftResetPosition;
            rightReset = checkpoint.RightResetPosition;
        }
    }
}