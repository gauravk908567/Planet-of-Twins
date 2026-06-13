using UnityEngine;

/// <summary>
/// Place a Trigger collider at the entrance of a puzzle area.
/// Calls QTEController.BeginQTE() when the required players enter.
///
/// FIX: explicitly excludes SoulPlayer so the soul flying through the zone
/// does not trigger the QTE. Only grounded Player instances count.
/// </summary>
public class QTEZoneTrigger : MonoBehaviour
{
    [SerializeField] private QTESceneAnchor qteAnchor;
    [Header("Tutorial � only fire during this stage (None = always)")]
    [SerializeField] private TutorialStage requiredStage = TutorialStage.None;

    [Tooltip("If true, waits for both players to enter before starting.")]
    [SerializeField] private bool requireBothPlayers = true;

    private int _playersInside = 0;
    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;

        // Stage guard � won't fire if player wanders in during wrong tutorial step
        if (requiredStage != TutorialStage.None &&
            TutorialContext.Instance?.CurrentStage != requiredStage) return;

        var player = other.GetComponent<Player>();
        if (player == null) return;

        // FIX: SoulPlayer extends Player � exclude it so a soul flying through
        // the zone does not start the QTE. Only the two physical twins count.
        if (player is SoulPlayer) return;

        _playersInside++;

        bool shouldStart = requireBothPlayers ? _playersInside >= 2 : _playersInside >= 1;
        if (shouldStart)
        {
            _triggered = true;
            qteAnchor?.BeginQTE();
            Debug.Log($"[QTEZoneTrigger] QTE triggered by players entering zone.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;
        _playersInside = Mathf.Max(0, _playersInside - 1);
    }

    public void ResetTrigger()
    {
        _triggered = false;
        _playersInside = 0;
    }
}