using UnityEngine;

/// <summary>
/// One large trigger collider covering the entire tutorial play area.
/// Active from tutorial start until TutorialStage.Complete.
///
/// When either twin exits this boundary, it asks TutorialContext for the
/// current stage, finds the matching TutorialBoundary zone in its list,
/// and calls TriggerReset() on it — placing the player back inside the
/// correct zone for the current tutorial step.
///
/// This means the player is never lost in a "completed zone with no boundary"
/// gap — they always get sent back to wherever they need to be right now.
///
/// SETUP:
///   Add to a GO with a large Box Collider (Is Trigger ON).
///   Size the collider to cover the ENTIRE tutorial area (park + gate + all zones).
///   Wire leftTwin and rightTwin.
///   Wire failureNotice — shown when outer boundary fires.
///   In zoneBoundaries[] drag every TutorialBoundary zone in the scene.
///   Order does not matter — lookup is by stage value.
///
/// NOTE:
///   Each TutorialBoundary in zoneBoundaries must have its activeStage set
///   to a unique stage. If two boundaries share the same stage, the first
///   match in the array wins.
/// </summary>
public class TutorialOuterBoundary : MonoBehaviour
{
    [Header("Twins")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Header("Failure notice — shown when outer boundary fires")]
    [SerializeField] private FailureNotice failureNotice;

    [Header("Message shown on outer boundary exit")]
    [SerializeField] private string boundaryMessage = "Head to the next area";

    [Header("All zone boundaries — one per tutorial stage")]
    [SerializeField] private TutorialBoundary[] zoneBoundaries;

    private bool _resetting = false;

    private void OnEnable() => _resetting = false;
    private void OnDisable() => _resetting = false;

    private void OnTriggerExit(Collider other)
    {
        if (_resetting) return;

        // Stop firing once tutorial is complete
        if (TutorialContext.Instance?.CurrentStage == TutorialStage.Complete) return;
        if (TutorialContext.Instance?.CurrentStage == TutorialStage.None) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;
        if (player != leftTwin && player != rightTwin) return;

        // Find the zone boundary matching the current stage
        var currentStage = TutorialContext.Instance.CurrentStage;
        TutorialBoundary activeBoundary = null;

        foreach (var boundary in zoneBoundaries)
        {
            if (boundary == null) continue;
            if (boundary.ActiveStage == currentStage)
            {
                activeBoundary = boundary;
                break;
            }
        }

        if (activeBoundary == null)
        {
            Debug.LogWarning(
                $"[TutorialOuterBoundary] No zone boundary found for stage {currentStage}. " +
                "Add a TutorialBoundary with that activeStage to zoneBoundaries[].", this);
            return;
        }

        // Show the outer message first, then delegate reset to the zone boundary
        // The zone boundary will show its own message too via TriggerReset()
        _resetting = true;
        failureNotice?.Show(boundaryMessage);

        // Small delay so the outer message is readable before the reset fires
        StartCoroutine(DelegateResetNextFrame(activeBoundary));
    }

    private System.Collections.IEnumerator DelegateResetNextFrame(TutorialBoundary boundary)
    {
        yield return new UnityEngine.WaitForSeconds(0.1f);
        boundary.TriggerReset();
        _resetting = false;
    }
}