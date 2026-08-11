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

        // ── Show prompt (or fail-loud skip if overlay missing) ────
        bool promptDone = false;
        if (ctx.overlay != null)
        {
            ctx.overlay.Show(
                promptTitle.IsEmpty ? "" : promptTitle.GetLocalizedString(),
                promptBody.IsEmpty ? "" : promptBody.GetLocalizedString(),
                promptClip, () => promptDone = true);
        }
        else
        {
            Debug.LogError("[TutorialRescueWatch] ctx.overlay is null — skipping explainer; " +
                           "rescue watch still proceeds.", this);
            promptDone = true;
        }

        // ── RACE: success vs. prompt dismissal ────────────────────
        // Rescue mash is input-driven (not deltaTime) so it completes at timeScale=0.
        // Never gate the success observation behind prompt dismissal.
        yield return new WaitUntil(() => promptDone || rescue.WasSuccessful);

        if (!promptDone)
            ctx.overlay.Continue();   // success beat the prompt — release the timeScale=0 hold

        if (rescue.WasSuccessful)
            yield break;

        // ── Failure watch — only reached when prompt dismissed first ──
        // TTK uses Time.deltaTime so failure cannot expire while overlay holds timeScale=0.
        string failMsg = failureMessage.IsEmpty
            ? "Reach your twin in time — move closer and mash F"
            : failureMessage.GetLocalizedString();

        while (true)
        {
            yield return null;

            if (rescue.WasSuccessful)
                yield break;

            if (rescue.CurrentRescueState == RescueState.Failed)
            {
                ctx.failureNotice?.Show(failMsg);
                ctx.resetSequencer?.TriggerReset(
                    ctx.RescueFailLeftReset,
                    ctx.RescueFailRightReset,
                    null);

                rescue.ResetSuccessFlag();

                yield return new WaitForSecondsRealtime(0.5f);
                yield return new WaitUntil(() => rescue.HasActiveRescueTarget);
            }
        }
    }
}