using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sits on CanvasEnemyUI — drives the rage/ritual timer sliders.
///
/// Emotion glyphs moved to the Manpu system (MANPU_SYSTEM.md) — the old Ikari mark + per-emotion
/// sprite/colour presets that used to live here are gone. This controller now owns only the two
/// world-space timer sliders.
///
/// SETUP:
///   Add to CanvasEnemyUI child GO. Wire _rageSlider, _ritualSlider in Inspector.
/// </summary>
public class EnemyStateUIController : MonoBehaviour
{
    [Header("Rage Timer Slider (depleting — orange)")]
    [SerializeField] private GameObject _rageSliderRoot;
    [SerializeField] private Slider _rageSlider;

    [Header("Ritual Timer Slider (filling — purple)")]
    [SerializeField] private GameObject _ritualSliderRoot;
    [SerializeField] private Slider _ritualSlider;

    private Coroutine _rageCoroutine;
    private Coroutine _ritualCoroutine;

    private void Awake()
    {
        _rageSliderRoot?.SetActive(false);
        _ritualSliderRoot?.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────────
    public void ShowRage(float duration)
    {
        if (_rageCoroutine != null) StopCoroutine(_rageCoroutine);
        _rageCoroutine = StartCoroutine(RageTimerRoutine(duration));
    }

    public void HideRage()
    {
        if (_rageCoroutine != null) { StopCoroutine(_rageCoroutine); _rageCoroutine = null; }
        _rageSliderRoot?.SetActive(false);
    }

    public void ShowRitual(float duration)
    {
        if (_ritualCoroutine != null) StopCoroutine(_ritualCoroutine);
        _ritualCoroutine = StartCoroutine(RitualTimerRoutine(duration));
    }

    public void HideRitual()
    {
        if (_ritualCoroutine != null) { StopCoroutine(_ritualCoroutine); _ritualCoroutine = null; }
        _ritualSliderRoot?.SetActive(false);
    }

    // ── Coroutines ─────────────────────────────────────────────
    private IEnumerator RageTimerRoutine(float duration)
    {
        _rageSliderRoot?.SetActive(true);
        if (_rageSlider != null)
        {
            _rageSlider.minValue = 0f;
            _rageSlider.maxValue = 1f;
            _rageSlider.value = 1f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_rageSlider != null)
                _rageSlider.value = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        _rageSliderRoot?.SetActive(false);
        _rageCoroutine = null;
    }

    private IEnumerator RitualTimerRoutine(float duration)
    {
        _ritualSliderRoot?.SetActive(true);
        if (_ritualSlider != null)
        {
            _ritualSlider.minValue = 0f;
            _ritualSlider.maxValue = 1f;
            _ritualSlider.value = 0f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_ritualSlider != null)
                _ritualSlider.value = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        _ritualSliderRoot?.SetActive(false);
        _ritualCoroutine = null;
    }
}
