using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Video;
// ═══════════════════════════════════════════════════════════════════
// TutorialPromptStepSO
// Shows overlay prompt. Completes when player clicks Continue.
// ═══════════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "Step_Prompt",
                 menuName = "PlanetOfTwins/Tutorial/Steps/Prompt")]
public class TutorialPromptStepSO : TutorialStepBase
{
    [Header("Prompt content")]
    public LocalizedString title;
    public LocalizedString body;
    public VideoClip previewClip;
    public string continueLabel = "Continue";

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);

        bool done = false;
        string t = title.IsEmpty ? "" : title.GetLocalizedString();
        string b = body.IsEmpty ? "" : body.GetLocalizedString();

        ctx.overlay?.Show(t, b, previewClip, () => done = true, continueLabel);
        yield return new WaitUntil(() => done);
    }
}
