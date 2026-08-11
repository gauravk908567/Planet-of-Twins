using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Valorant-style failure reset visual (P17: rides the dedicated FailureSting Volume).
/// Sting-in → black → teleport → sting-out.
///
/// _postProcessVolume is a DEDICATED global Volume at priority 30 whose profile
/// (FailureReset_Sting) carries the whole look — desat −80, vignette 0.45, CA 0.25
///. This sequencer only animates the volume WEIGHT 0→1→0, so
/// the sting never fights the StoryGradeDirector volumes (priority 0) and needs no
/// runtime profile mutation. Also uses a fullscreen black Image for the cut-to-black.
///
/// SETUP:
///   Wire _postProcessVolume → the FailureSting Volume (priority 30, weight 0).
///   Wire _blackOverlay → a full-screen Image on a high sort-order canvas (alpha 0).
///   TutorialDirector calls TriggerReset(leftPos, rightPos, onComplete).
/// </summary>
public class FailureResetSequencer : MonoBehaviour
{
    public static FailureResetSequencer Instance { get; private set; }

    [Header("Post Process")]
    [Tooltip("Dedicated FailureSting Volume (global, priority 30, weight 0 at rest). Its profile holds the full sting look.")]
    [SerializeField] private Volume _postProcessVolume;

    [Header("Black overlay")]
    [Tooltip("Full-screen Image on a canvas above everything else. Alpha 0 at start.")]
    [SerializeField] private Image _blackOverlay;

    [Header("Twins")]
    [SerializeField] private Player _leftTwin;
    [SerializeField] private Player _rightTwin;

    [Header("Timing")]
    [SerializeField] private float _greyscaleDuration = 0.4f;
    [SerializeField] private float _holdGreyscale = 0.15f;
    [SerializeField] private float _fadeToBlackDuration = 0.3f;
    [SerializeField] private float _holdBlack = 0.2f;
    [SerializeField] private float _restoreColour = 0.3f;

    [Header("Teleport ground offset")]
    [SerializeField] private float teleportYOffset = 0.5f;

    private Coroutine _activeReset;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_postProcessVolume != null)
            _postProcessVolume.weight = 0f;   // sting fully off at rest
        else
            Debug.LogError("[FailureResetSequencer] _postProcessVolume unwired — the failure sting will play nothing.", this);

        if (_blackOverlay != null)
        {
            var c = _blackOverlay.color;
            c.a = 0f;
            _blackOverlay.color = c;
            _blackOverlay.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Play the reset sequence. Teleports both twins to the given positions.
    /// onComplete fires after colour is restored — use to re-enable input, show notice etc.
    /// Re-entry-rejecting: a new call while a reset is running is silently ignored (callers guard).
    /// </summary>
    public void TriggerReset(Vector3 leftResetPos, Vector3 rightResetPos,
                             System.Action onComplete = null)
    {
        if (_activeReset != null)
        {
            Debug.LogWarning("[FailureResetSequencer] reset already running — ignored", this);
            return;
        }
        _activeReset = StartCoroutine(ResetSequence(leftResetPos, rightResetPos, onComplete));
    }

    private IEnumerator ResetSequence(Vector3 leftPos, Vector3 rightPos,
                                      System.Action onComplete)
    {
        // ── 1. Sting in (volume weight 0→1: desat + vignette + CA together) ──
        if (_postProcessVolume != null)
        {
            float elapsed = 0f; // unscaled — plays while gameplay is halted
            while (elapsed < _greyscaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _postProcessVolume.weight = Mathf.Clamp01(elapsed / _greyscaleDuration);
                yield return null;
            }
            _postProcessVolume.weight = 1f;
        }

        yield return new WaitForSecondsRealtime(_holdGreyscale);

        // ── 2. Fade to black ──────────────────────────────────
        if (_blackOverlay != null)
        {
            _blackOverlay.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < _fadeToBlackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBlackAlpha(Mathf.Clamp01(elapsed / _fadeToBlackDuration));
                yield return null;
            }
            SetBlackAlpha(1f);
        }

        yield return new WaitForSecondsRealtime(_holdBlack);

        // ── 3. Teleport ───────────────────────────────────────
        TeleportTwin(_leftTwin, leftPos);
        TeleportTwin(_rightTwin, rightPos);

        // ── 4. Fade out black + restore colour ────────────────
        float restoreElapsed = 0f;
        while (restoreElapsed < _restoreColour)
        {
            restoreElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(restoreElapsed / _restoreColour);
            SetBlackAlpha(1f - t);
            if (_postProcessVolume != null)
                _postProcessVolume.weight = 1f - t;
            yield return null;
        }

        SetBlackAlpha(0f);
        if (_blackOverlay != null) _blackOverlay.gameObject.SetActive(false);
        if (_postProcessVolume != null) _postProcessVolume.weight = 0f;

        _activeReset = null;
        onComplete?.Invoke();
    }

    private void TeleportTwin(Player twin, Vector3 pos)
    {
        if (twin == null) return;
        var cc = twin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        twin.transform.position = pos + Vector3.up * teleportYOffset;
        if (cc != null) cc.enabled = true;
    }

    private void SetBlackAlpha(float a)
    {
        if (_blackOverlay == null) return;
        var c = _blackOverlay.color;
        c.a = a;
        _blackOverlay.color = c;
    }
}