using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small transient warning banner — Valorant style.
/// Auto-dismisses after displayDuration seconds.
/// Stack-safe: calling Show() while visible restarts the timer.
///
/// SETUP:
///   Add to a canvas panel at the top of screen.
///   NoticePanel starts inactive.
///   Wire NoticeText.
///   Optionally wire CanvasGroup for fade.
///
/// HIERARCHY:
///   ValorantNotice (this script)
///     └── NoticePanel (Image, ~600x60, top-centre)
///           ├── WarningIcon  (Image, small red circle with !)
///           └── NoticeText   (TMP, left-aligned)
/// </summary>
public class FailureNotice : MonoBehaviour
{
    public static FailureNotice Instance { get; private set; }

    [SerializeField] private GameObject _noticePanel;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _displayDuration = 2.5f;
    [SerializeField] private float _fadeDuration = 0.3f;

    private Coroutine _active;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _noticePanel?.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (_noticeText != null) _noticeText.text = message;
        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        _noticePanel?.SetActive(true);

        yield return new WaitForSecondsRealtime(_displayDuration);

        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
        }

        _noticePanel?.SetActive(false);
        _active = null;
    }
}