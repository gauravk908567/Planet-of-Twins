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

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);

        if (lockInputDuringPlayback)
        {
            ctx.inputGate?.LockAll();
            ctx.SelectionLock?.LockSelection();
        }

        if (ctx.timeline != null)
        {
            ctx.timeline.Play();
            yield return new WaitUntil(() => ctx.timeline.state != PlayState.Playing);
        }

        if (lockInputDuringPlayback)
            ctx.SelectionLock?.UnlockSelection();
    }
}
