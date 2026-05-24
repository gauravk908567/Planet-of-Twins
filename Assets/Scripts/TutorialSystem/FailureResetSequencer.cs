using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Valorant-style failure reset visual.
/// Greyscale → black → teleport → colour restore.
///
/// Requires a Volume with ColorAdjustments override in the scene.
/// Also uses a fullscreen black Image for the cut-to-black step.
///
/// SETUP:
///   Wire _postProcessVolume → your main post-process Volume GO.
///   Wire _blackOverlay → a full-screen Image on a high sort-order canvas (alpha 0).
///   TutorialDirector calls TriggerReset(leftPos, rightPos, onComplete).
/// </summary>
public class FailureResetSequencer : MonoBehaviour
{
    [Header("Post Process")]
    [Tooltip("Main post-process Volume that has a ColorAdjustments override.")]
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

    private ColorAdjustments _colourAdj;
    private float _originalSaturation;
    private Coroutine _activeReset;

    private void Awake()
    {
        if (_postProcessVolume != null &&
            _postProcessVolume.profile.TryGet(out _colourAdj))
        {
            _originalSaturation = _colourAdj.saturation.value;
        }

        if (_blackOverlay != null)
        {
            var c = _blackOverlay.color;
            c.a = 0f;
            _blackOverlay.color = c;
            _blackOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Play the reset sequence. Teleports both twins to the given positions.
    /// onComplete fires after colour is restored — use to re-enable input, show Valorant notice etc.
    /// </summary>
    public void TriggerReset(Vector3 leftResetPos, Vector3 rightResetPos,
                             System.Action onComplete = null)
    {
        if (_activeReset != null) StopCoroutine(_activeReset);
        _activeReset = StartCoroutine(ResetSequence(leftResetPos, rightResetPos, onComplete));
    }

    private IEnumerator ResetSequence(Vector3 leftPos, Vector3 rightPos,
                                      System.Action onComplete)
    {
        // ── 1. Greyscale ──────────────────────────────────────
        if (_colourAdj != null)
        {
            float elapsed = 0f;
            while (elapsed < _greyscaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _greyscaleDuration);
                _colourAdj.saturation.Override(Mathf.Lerp(_originalSaturation, -100f, t));
                yield return null;
            }
            _colourAdj.saturation.Override(-100f);
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
            if (_colourAdj != null)
                _colourAdj.saturation.Override(Mathf.Lerp(-100f, _originalSaturation, t));
            yield return null;
        }

        SetBlackAlpha(0f);
        if (_blackOverlay != null) _blackOverlay.gameObject.SetActive(false);
        if (_colourAdj != null) _colourAdj.saturation.Override(_originalSaturation);

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