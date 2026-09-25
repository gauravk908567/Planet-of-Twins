using UnityEngine;

/// <summary>
/// Lightweight one-shot watcher added at runtime by TutorialQTEStepSO.
/// Subscribes to QTEManager.OnQTESucceeded and fires onSuccess when the
/// expected event ID succeeds. Filtered by eventId so it only responds to
/// the QTE that was just started.
/// </summary>
public class QTESuccessWatcher : MonoBehaviour
{
    private string _expectedEventId;
    private System.Action _onSuccess;
    private bool _watching;

    public void Watch(string expectedEventId, System.Action onSuccess)
    {
        _expectedEventId = expectedEventId;
        _onSuccess = onSuccess;
        _watching = true;

        if (QTEManager.Instance != null)
            QTEManager.Instance.OnQTESucceeded += HandleSucceeded;
        else
            Debug.LogWarning("[QTESuccessWatcher] QTEManager not found. " +
                             "Is it in Persistent.unity?", this);
    }

    private void HandleSucceeded(string eventId)
    {
        if (!_watching) return;
        if (eventId != _expectedEventId) return;

        _watching = false;
        if (QTEManager.Instance != null)
            QTEManager.Instance.OnQTESucceeded -= HandleSucceeded;
        _onSuccess?.Invoke();
    }

    private void OnDestroy()
    {
        if (QTEManager.Instance != null)
            QTEManager.Instance.OnQTESucceeded -= HandleSucceeded;
    }
}
