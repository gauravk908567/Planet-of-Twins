using UnityEngine;

/// <summary>
/// Lightweight one-shot watcher added at runtime by TutorialQTEStepSO.
/// Polls QTEController.IsSuccess every frame.
/// Fires onSuccess callback and stops watching on first success.
/// Destroyed by TutorialQTEStepSO after callback fires.
/// </summary>
public class QTESuccessWatcher : MonoBehaviour
{
    private QTEController _controller;
    private System.Action _onSuccess;
    private bool _watching;

    public void Watch(QTEController controller, System.Action onSuccess)
    {
        _controller = controller;
        _onSuccess = onSuccess;
        _watching = true;
    }

    private void Update()
    {
        if (!_watching || _controller == null) return;
        if (_controller.IsSuccess)
        {
            _watching = false;
            _onSuccess?.Invoke();
        }
    }
}