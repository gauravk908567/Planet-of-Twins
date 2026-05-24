using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space subtitle bar. Receives speakerName (string) and speakerColour (Color).
/// Knows nothing about DialogueSpeaker enum, Lyra, Kai, or game logic.
/// Used exclusively by DialoguePlayer — not by AbilityFeedbackDisplay.
/// </summary>
public class SubtitleBarDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _speakerText;
    [SerializeField] private TMP_Text _lineText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _barRect;

    [Header("Animation")]
    [SerializeField] private float _slideInDuration = 0.14f;
    [SerializeField] private float _slideOutDuration = 0.18f;
    [SerializeField] private float _slideOffset = 40f;

    private Coroutine _current;

    // ── Public API ────────────────────────────────────────────
    /// <param name="speakerName">Display name of speaker — set by DialoguePlayer.</param>
    /// <param name="speakerColour">Colour for speaker name label — set by DialoguePlayer.</param>
    public void Show(string speakerName, Color speakerColour, string text,
                     float duration, System.Action onComplete = null)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(ShowRoutine(speakerName, speakerColour, text, duration, onComplete));
    }

    public void Hide()
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(SlideOut(null));
    }

    // ── Internal ──────────────────────────────────────────────
    private IEnumerator ShowRoutine(string speakerName, Color speakerColour,
                                     string text, float duration,
                                     System.Action onComplete)
    {
        if (_speakerText != null)
        {
            _speakerText.text = speakerName;
            _speakerText.color = speakerColour;
            _speakerText.gameObject.SetActive(!string.IsNullOrEmpty(speakerName));
        }

        if (_lineText != null) _lineText.text = text;

        _root.SetActive(true);
        yield return StartCoroutine(SlideIn());
        yield return new WaitForSeconds(duration);
        yield return StartCoroutine(SlideOut(onComplete));
        _root.SetActive(false);
        _current = null;
    }

    private IEnumerator SlideIn()
    {
        if (_barRect == null && _canvasGroup == null) yield break;
        float elapsed = 0f;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        Vector2 startPos = _barRect != null ? _barRect.anchoredPosition + new Vector2(0, -_slideOffset) : Vector2.zero;
        Vector2 endPos = _barRect != null ? _barRect.anchoredPosition : Vector2.zero;

        while (elapsed < _slideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / _slideInDuration), 3f);
            if (_barRect != null) _barRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            if (_canvasGroup != null) _canvasGroup.alpha = t;
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private IEnumerator SlideOut(System.Action onComplete)
    {
        float elapsed = 0f;
        Vector2 startPos = _barRect != null ? _barRect.anchoredPosition : Vector2.zero;
        Vector2 endPos = startPos + new Vector2(0, -_slideOffset);

        while (elapsed < _slideOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _slideOutDuration);
            if (_barRect != null) _barRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            if (_canvasGroup != null) _canvasGroup.alpha = 1f - t;
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        onComplete?.Invoke();
    }
}