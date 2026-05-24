using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// World-space nameplate above a twin's head.
/// Used by two separate systems — must not conflict:
///
///   DialoguePlayer      → ShowDialogue(text, duration)
///   AbilityFeedbackDisplay → ShowAbility(text, duration)
///
/// Priority: DialoguePlayer has higher priority.
/// If a dialogue line is showing, ability feedback is suppressed.
/// If ability feedback is showing and dialogue starts, dialogue takes over.
///
/// SETUP:
///   Add to a child GO of the twin prefab.
///   Position above head (e.g. localPosition 0, 2.2, 0).
///   Billboard faces camera — Canvas is World Space, set Camera in Inspector.
///   NameplateRoot disabled by default.
///
/// HIERARCHY:
///   NameplateRoot (this script)
///     └── Canvas (World Space, Camera = MainCamera)
///           └── Panel (Image, rounded, semi-transparent)
///                 ├── SpeakerNameText (TMP, 11pt, muted)
///                 └── LineText (TMP, 14pt)
/// </summary>
public class NameplateDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _speakerName;
    [SerializeField] private TMP_Text _lineText;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Fade")]
    [SerializeField] private float _fadeInDuration = 0.12f;
    [SerializeField] private float _fadeOutDuration = 0.2f;

    // ── State ─────────────────────────────────────────────────
    private bool _dialogueActive = false;  // set by DialoguePlayer
    private Coroutine _current;

    // ── DialoguePlayer API ────────────────────────────────────
    public void ShowDialogue(string speaker, string text, float duration)
    {
        _dialogueActive = true;
        ShowInternal(speaker, text, duration, onComplete: () => _dialogueActive = false);
    }

    public void HideDialogue()
    {
        _dialogueActive = false;
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(FadeOut());
    }

    // ── AbilityFeedbackDisplay API ────────────────────────────
    /// <summary>
    /// Called by AbilityFeedbackDisplay. Suppressed if dialogue is active.
    /// Returns true if the ability text was shown, false if suppressed.
    /// </summary>
    public bool ShowAbilityFeedback(string text, float duration)
    {
        if (_dialogueActive) return false;   // dialogue has priority — don't conflict
        ShowInternal("", text, duration, onComplete: null);
        return true;
    }

    // ── Internal ──────────────────────────────────────────────
    private void ShowInternal(string speaker, string text, float duration,
                               System.Action onComplete)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(ShowRoutine(speaker, text, duration, onComplete));
    }

    private IEnumerator ShowRoutine(string speaker, string text,
                                     float duration, System.Action onComplete)
    {
        if (_speakerName != null)
        {
            _speakerName.text = speaker;
            _speakerName.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
        }

        if (_lineText != null) _lineText.text = text;

        _root.SetActive(true);
        yield return StartCoroutine(FadeIn());

        yield return new WaitForSeconds(duration);

        yield return StartCoroutine(FadeOut());
        _root.SetActive(false);

        _current = null;
        onComplete?.Invoke();
    }

    private IEnumerator FadeIn()
    {
        if (_canvasGroup == null) yield break;
        float elapsed = 0f;
        _canvasGroup.alpha = 0f;
        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        if (_canvasGroup == null) yield break;
        float elapsed = 0f;
        float start = _canvasGroup.alpha;
        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / _fadeOutDuration);
            yield return null;
        }
        _canvasGroup.alpha = 0f;
    }
}