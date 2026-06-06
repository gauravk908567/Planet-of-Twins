using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives CanvasGroup alpha for fade in/out.
/// Call FadeIn() / FadeOut() from a Timeline SignalReceiver,
/// or from code directly (optional callback on complete).
///
/// Setup:
///   - Attach to FadeCanvas
///   - Assign the CanvasGroup in Inspector (or it auto-finds on Awake)
///   - In Timeline: add Signal Emitter → wire to FadeIn() or FadeOut()
/// </summary>
public class FadeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Timing")]
    [Tooltip("Duration of a full fade in or fade out")]
    [SerializeField] private float _fadeDuration = 0.5f;

    private Coroutine _activeCoroutine;

    // ── Unity ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
    }

    // ── Public API — call these from Timeline SignalReceiver ─────────────

    /// <summary>Fade from current alpha → 1 (fully opaque / black screen)</summary>
    public void FadeIn() => FadeIn(_fadeDuration, null);

    /// <summary>Fade from current alpha → 0 (fully transparent / game visible)</summary>
    public void FadeOut() => FadeOut(_fadeDuration, null);

    /// <summary>Fade in then immediately fade out — clean flash cut</summary>
    public void FadeInThenOut() => StartCoroutine(FadeInThenOutRoutine(_fadeDuration));

    /// <summary>
    /// Snap to black instantly, then fade out to reveal the game.
    /// Use this at the START of a cinematic to reveal the world.
    /// Single signal reaction — no ordering issue.
    /// </summary>
    public void StartFromBlack()
    {
        StopActive();
        SetAlpha(1f);
        _activeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, _fadeDuration, null));
    }

    /// <summary>
    /// Snap to clear instantly, then fade in to black.
    /// Use this at the END of a cinematic to hide the world.
    /// Single signal reaction — no ordering issue.
    /// </summary>
    public void StartFromClear()
    {
        StopActive();
        SetAlpha(0f);
        _activeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, _fadeDuration, null));
    }

    // ── Public API — call from code with callback ────────────────────────

    public void FadeIn(float duration, Action onComplete)
    {
        StopActive();
        _activeCoroutine = StartCoroutine(FadeRoutine(
            _canvasGroup.alpha, 1f, duration, onComplete));
    }

    public void FadeOut(float duration, Action onComplete)
    {
        StopActive();
        _activeCoroutine = StartCoroutine(FadeRoutine(
            _canvasGroup.alpha, 0f, duration, onComplete));
    }

    /// <summary>Skip animation — snap to black immediately</summary>
    public void SetBlack()
    {
        StopActive();
        SetAlpha(1f);
    }

    /// <summary>Skip animation — snap to clear immediately</summary>
    public void SetClear()
    {
        StopActive();
        SetAlpha(0f);
    }

    // ── Private ──────────────────────────────────────────────────────────

    private IEnumerator FadeRoutine(float from, float to, float duration, Action onComplete)
    {
        SetAlpha(from);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;           // unscaled: works during timeScale = 0
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        SetAlpha(to);
        onComplete?.Invoke();
        _activeCoroutine = null;
    }

    private IEnumerator FadeInThenOutRoutine(float halfDuration)
    {
        yield return FadeRoutine(_canvasGroup.alpha, 1f, halfDuration, null);
        yield return FadeRoutine(1f, 0f, halfDuration, null);
    }

    private void SetAlpha(float alpha)
    {
        _canvasGroup.alpha = alpha;
        _canvasGroup.blocksRaycasts = alpha > 0.99f; // block input only when fully black
        _canvasGroup.interactable = false;
    }

    private void StopActive()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }
    }
}