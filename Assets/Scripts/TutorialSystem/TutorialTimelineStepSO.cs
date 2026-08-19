using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
// ═══════════════════════════════════════════════════════════════════
// TutorialTimelineStepSO
// Plays a PlayableDirector. Waits for it to finish.
// Optionally locks input during playback.
// ═══════════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "Step_Timeline",
                 menuName = "PlanetOfTwins/Tutorial/Steps/Timeline")]
public class TutorialTimelineStepSO : TutorialStepBase
{
    [Header("Playback")]
    [Tooltip("If true, locks all input and twin selection during timeline.")]
    public bool lockInputDuringPlayback = true;

    [Header("Cutscene end → game (white fade-in)")]
    [Tooltip("When the timeline ends the screen is white. We restore the gameplay cameras' authored rotation " +
             "BEHIND the white (fixes the recurring flip), then fade white→game over this duration before " +
             "handing control to the player. The camera correction is never seen because the screen is opaque.")]
    [Min(0f)] public float whiteFadeInDuration = 2.3f;

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);

        if (lockInputDuringPlayback)
        {
            ctx.inputGate?.LockAll();
        }

        if (ctx.timeline != null)
        {
            ctx.timeline.Play();
            yield return new WaitUntil(() => ctx.timeline.state != PlayState.Playing);
        }

        // ── Cutscene end: screen is white. Correct the cameras behind it, then fade in to the game. ──
        // Restore authored camera rotations (flip fix) while the white still covers the screen — the snap is
        // invisible. Then fade white→game over whiteFadeInDuration; control returns to the player after.
        ctx.cameraGuard?.RestoreAll();

        if (ctx.fade != null)
            yield return FadeWhiteToGame(ctx, executor);
    }

    // Hold the cover, then fade to clear over whiteFadeInDuration. The wait is BOUNDED BY TIME, not by the
    // FadeOut callback: if the FadeController's coroutine ever fails to complete (null CanvasGroup, GO disabled),
    // the callback never fires — gating on it hung the step and left the screen permanently opaque (the
    // "boots into white" regression). We instead wait the duration ourselves and ALWAYS SetClear() at the end,
    // so the game is guaranteed visible regardless of the fade animation's fate. Unscaled (timeScale-safe).
    private IEnumerator FadeWhiteToGame(TutorialStepContext ctx, MonoBehaviour executor)
    {
        // NOTE: SetBlack() only sets ALPHA→1 (fully opaque). The cover COLOR is the FadeImage's color, which is
        // WHITE (RGBA 1,1,1,1) — so this shows a white screen, matching the cutscene's white end frame. The
        // method name is legacy ("black") but the rendered cover is white.
        ctx.fade.SetBlack();                       // alpha→1: raise the (white) cover before revealing
        ctx.fade.FadeOut(whiteFadeInDuration, null);
        if (whiteFadeInDuration > 0f)
            yield return new WaitForSecondsRealtime(whiteFadeInDuration);
        ctx.fade.SetClear();                       // hard-guarantee the screen ends transparent
    }
}
