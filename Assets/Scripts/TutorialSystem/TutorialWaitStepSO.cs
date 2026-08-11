using System.Collections;
using UnityEngine;
// ═══════════════════════════════════════════════════════════════════
// TutorialWaitStepSO
// Waits a fixed number of seconds. No interaction.
// ═══════════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "Step_Wait",
                 menuName = "PlanetOfTwins/Tutorial/Steps/Wait")]
public class TutorialWaitStepSO : TutorialStepBase
{
    [Min(0f)]
    public float waitSeconds = 1f;

    public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
    {
        ApplyCommonSetup(ctx);
        yield return new WaitForSeconds(waitSeconds);
    }
}
