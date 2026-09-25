using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
// ═══════════════════════════════════════════════════════════════════
// TutorialUnlockAllStepSO
// Unlocks all input and marks tutorial complete. Always last step.
// ═══════════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "Step_UnlockAll",
                 menuName = "PlanetOfTwins/Tutorial/Steps/UnlockAll")]
public class TutorialUnlockAllStepSO : TutorialStepBase
{
    [Header("Final prompt (optional)")]
    public LocalizedString finalTitle;
    public LocalizedString finalBody;
    public string continueLabel = "Let's go";

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        // Show final prompt if content provided
        if (!finalTitle.IsEmpty || !finalBody.IsEmpty)
        {
            bool done = false;
            string t = finalTitle.IsEmpty ? "" : finalTitle.GetLocalizedString();
            string b = finalBody.IsEmpty ? "" : finalBody.GetLocalizedString();
            ctx.overlay?.Show(t, b, null, () => done = true, continueLabel);
            yield return new WaitUntil(() => done);
        }

        ctx.inputGate?.AllowAll();
        TutorialContext.Instance?.SetStage(TutorialStage.Complete);
    }
}