using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Video;

// ═══════════════════════════════════════════════════════════════════
// TutorialRescueWatchStepSO
// Waits for any twin to get grabbed (HasActiveRescueTarget).
// Shows prompt immediately when grab fires.
// Polls WasSuccessful (latched flag) every frame — never misses success
// even though RescueEventController resets state to Idle immediately.
// On failure — resets twins to rescue reset points.
// ═══════════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "Step_RescueWatch",
                 menuName = "PlanetOfTwins/Tutorial/Steps/RescueWatch")]
public class TutorialRescueWatchStepSO : TutorialStepBase
{
    [Header("Prompt — shown as soon as twin is grabbed")]
    public LocalizedString promptTitle;
    public LocalizedString promptBody;
    public VideoClip promptClip;

    [Header("Failure notice")]
    public LocalizedString failureMessage;

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);

        var rescue = ctx.RescueProvider;
        if (rescue == null)
        {
            Debug.LogError("[TutorialRescueWatch] RescueProvider is null.", this);
            yield break;
        }

        // Reset success latch before watching
        rescue.ResetSuccessFlag();

        // ── Wait for twin to get grabbed ──────────────────────────
        yield return new WaitUntil(() => rescue.HasActiveRescueTarget);

        // ── Show prompt ───────────────────────────────────────────
        bool promptDone = false;
        string t = promptTitle.IsEmpty ? "" : promptTitle.GetLocalizedString();
        string b = promptBody.IsEmpty ? "" : promptBody.GetLocalizedString();
        ctx.overlay?.Show(t, b, promptClip, () => promptDone = true);
        yield return new WaitUntil(() => promptDone);

        // ── Watch outcome every frame — never miss success ────────
        string failMsg = failureMessage.IsEmpty
            ? "Reach your twin in time — move closer and mash F"
            : failureMessage.GetLocalizedString();

        while (true)
        {
            yield return null; // poll every frame, not every 0.1s
            Debug.Log($"[RescueWatch] WasSuccessful={rescue.WasSuccessful} State={rescue.CurrentRescueState}");

            // WasSuccessful latches true and stays true even after
            // RescueEventController resets CurrentRescueState to Idle
            if (rescue.WasSuccessful)
                yield break;

            if (rescue.CurrentRescueState == RescueState.Failed)
            {
                ctx.failureNotice?.Show(failMsg);
                ctx.resetSequencer?.TriggerReset(
                    ctx.RescueFailLeftReset,
                    ctx.RescueFailRightReset,
                    null);

                // Reset latch for retry
                rescue.ResetSuccessFlag();

                // Wait for trap to grab again
                yield return new WaitForSeconds(0.5f);
                yield return new WaitUntil(() => rescue.HasActiveRescueTarget);
            }
        }
    }
}