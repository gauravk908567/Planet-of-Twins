using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "Step_QTE",
                 menuName = "PlanetOfTwins/Tutorial/Steps/QTE")]
public class TutorialQTEStepSO : TutorialStepBase
{
    [Header("Prompt shown before QTE begins")]
    public LocalizedString promptTitle;
    public LocalizedString promptBody;
    public VideoClip promptClip;

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);

        // Show overlay prompt first — time pauses, player watches the video
        bool promptDone = false;
        string t = promptTitle.IsEmpty ? "" : promptTitle.GetLocalizedString();
        string b = promptBody.IsEmpty ? "" : promptBody.GetLocalizedString();
        ctx.overlay?.Show(t, b, promptClip, () => promptDone = true);
        yield return new WaitUntil(() => promptDone);

        // Unlock Interact (F / pad South) BEFORE arming the QTE. Lock-in reads GetInteractDown, which is a
        // SEPARATE gate category from Rescue — without this the "Press F" prompt shows but F is gated off, so
        // the trigger points never lock in and the QTE can't start (the "QTE press F not working" bug, all devices).
        ctx.inputGate?.AllowInteract(true);

        // Start the QTE — camera switches here, trigger points activate
        ctx.qteAnchor?.BeginQTE();

        // Listen for success via watcher (filtered by this QTE's eventId)
        bool gateOpened = false;
        var watcher = executor.gameObject.AddComponent<QTESuccessWatcher>();
        watcher.Watch(ctx.qteAnchor?.Definition?.eventId, () => gateOpened = true);

        yield return new WaitUntil(() => gateOpened);

        Object.Destroy(watcher);
    }
}
