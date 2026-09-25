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
        if (_canvasGroup == null)
        {
            Debug.LogError("[FadeController] No CanvasGroup — fades will throw and can strand the screen opaque. " +
                           "Assign one on the FadeCanvas.", this);
            return;
        }
        // Start CLEAR (alpha 0) so the fade canvas never covers the game until something explicitly asks it to.
        // Authoring the canvas opaque in the scene was the "boots into white" bug — the cover stayed up because
        // nothing cleared it. The cover is raised intentionally (e.g. timeline end → SetBlack → fade to 0).
        SetAlpha(0f);
    }

    // A Timeline Activation track (tutorial director, track 8) can leave this GO INACTIVE after a
    // cutscene — every later fade then threw "Coroutine couldn't be started because … 'FadeCanvas'
    // is inactive" (BUG-071). The canvas is invisible at alpha 0 anyway, so any fade request may
    // safely re-activate it. Called by every public entry point before StartCoroutine.
    private bool EnsureActiveForFade()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[FadeController] FadeCanvas has an inactive PARENT — fade skipped. " +
                           "Keep the fade canvas out of Activation-Track hierarchies (R11).", this);
            return false;
        }
        return true;
    }

    // ── Public API — call these from Timeline SignalReceiver ─────────────

    /// <summary>Fade from current alpha → 1 (fully opaque / black screen)</summary>
    public void FadeIn() => FadeIn(_fadeDuration, null);

    /// <summary>Fade from current alpha → 0 (fully transparent / game visible)</summary>
    public void FadeOut() => FadeOut(_fadeDuration, null);

    /// <summary>Fade in then immediately fade out — clean flash cut</summary>
    public void FadeInThenOut()
    {
        if (!EnsureActiveForFade()) return;
        StartCoroutine(FadeInThenOutRoutine(_fadeDuration));
    }

    /// <summary>
    /// Snap to black instantly, then fade out to reveal the game.
    /// Use this at the START of a cinematic to reveal the world.
    /// Single signal reaction — no ordering issue.
    /// </summary>
    public void StartFromBlack()
    {
        if (!EnsureActiveForFade()) return;
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
        if (!EnsureActiveForFade()) return;
        StopActive();
        SetAlpha(0f);
        _activeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, _fadeDuration, null));
    }

    // ── Public API — call from code with callback ────────────────────────

    public void FadeIn(float duration, Action onComplete)
    {
        if (!EnsureActiveForFade()) { onComplete?.Invoke(); return; }
        StopActive();
        _activeCoroutine = StartCoroutine(FadeRoutine(
            _canvasGroup.alpha, 1f, duration, onComplete));
    }

    public void FadeOut(float duration, Action onComplete)
    {
        if (!EnsureActiveForFade()) { onComplete?.Invoke(); return; }
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
        if (_canvasGroup == null) return;            // guard: a null group must not throw mid-coroutine (would
                                                     // kill the routine before it clears → screen stuck opaque)
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