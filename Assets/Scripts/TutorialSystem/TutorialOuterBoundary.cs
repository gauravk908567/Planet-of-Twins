using UnityEngine;

/// <summary>
/// One large trigger collider covering the entire tutorial play area.
/// Active from tutorial start until TutorialStage.Complete.
///
/// When either twin exits this boundary, it asks TutorialContext for the
/// current stage, finds the matching TutorialBoundary zone in its list,
/// and calls TriggerReset() on it - placing the player back inside the
/// correct zone for the current tutorial step.
///
/// SETUP:
///   Add to a GO with a large Box Collider (Is Trigger ON).
///   Size the collider to cover the ENTIRE tutorial area (park + gate + all zones).
///   In zoneBoundaries[] drag every TutorialBoundary zone in the scene.
///   Order does not matter - lookup is by stage value.
/// </summary>
public class TutorialOuterBoundary : MonoBehaviour
{
    [Header("Message shown on outer boundary exit")]
    [SerializeField] private string boundaryMessage = "Head to the next area";

    [Header("All zone boundaries - one per tutorial stage")]
    [SerializeField] private TutorialBoundary[] zoneBoundaries;

    // Runtime resolved (Persistent singletons - R4)
    private Player _leftTwin;
    private Player _rightTwin;
    private FailureNotice _failureNotice;

    private void Start()
    {
        _failureNotice = FailureNotice.Instance;
        var selector = TwinSelector.Instance;
        if (selector != null)
        {
            _leftTwin = selector.LeftTwin;
            _rightTwin = selector.RightTwin;
        }
        if (_failureNotice == null)
            Debug.LogError("[TutorialOuterBoundary] FailureNotice.Instance is null — is Persistent loaded?", this);
        if (_leftTwin == null || _rightTwin == null)
            Debug.LogError("[TutorialOuterBoundary] Twins unresolved via TwinSelector.Instance — is Persistent loaded?", this);
    }

    private void OnTriggerExit(Collider other)
    {
        // Stop firing once tutorial is complete
        if (TutorialContext.Instance?.CurrentStage == TutorialStage.Complete) return;
        if (TutorialContext.Instance?.CurrentStage == TutorialStage.None) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;
        if (player != _leftTwin && player != _rightTwin) return;

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

        // Show the outer message first, then delegate reset to the zone boundary.
        // Guard lives in TutorialBoundary.TriggerReset() and the re-entry-rejecting sequencer.
        _failureNotice?.Show(boundaryMessage);
        StartCoroutine(DelegateResetAfterDelay(activeBoundary));
    }

    private System.Collections.IEnumerator DelegateResetAfterDelay(TutorialBoundary boundary)
    {
        yield return new WaitForSecondsRealtime(0.1f);
        boundary.TriggerReset();
    }
}
