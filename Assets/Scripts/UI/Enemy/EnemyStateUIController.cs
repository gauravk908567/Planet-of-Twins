using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sits on CanvasEnemyUI — drives rage/ritual sliders.
/// Ikari mark is spawned as a world-space prefab via ShowIkari().
///
/// SETUP:
///   Add to CanvasEnemyUI child GO.
///   Wire _rageSlider, _ritualSlider in Inspector.
///   Wire _ikariPrefab to your IkariMarkVFX prefab.
///   _ikariOffset: how high above enemy root to spawn mark (e.g. 2.5f).
/// </summary>
public class EnemyStateUIController : MonoBehaviour
{
    [Header("Rage Timer Slider (depleting — orange)")]
    [SerializeField] private GameObject _rageSliderRoot;
    [SerializeField] private Slider _rageSlider;

    [Header("Ritual Timer Slider (filling — purple)")]
    [SerializeField] private GameObject _ritualSliderRoot;
    [SerializeField] private Slider _ritualSlider;

    [Header("Ikari Anger Mark — one GO, 3 states")]
    [SerializeField] private IkariMarkVFX _ikariMark;

    [Header("Rage — pulses between red and black")]
    [SerializeField] private Sprite _rageSprite;
    [SerializeField] private Color _rageColourA = new Color(1f, 0.05f, 0f, 1f);   // red
    [SerializeField] private Color _rageColourB = new Color(0.1f, 0f, 0f, 1f);    // near black

    [Header("Fear — pulses between blue and dark blue")]
    [SerializeField] private Sprite _fearSprite;
    [SerializeField] private Color _fearColourA = new Color(0.3f, 0.6f, 1f, 1f);  // bright blue
    [SerializeField] private Color _fearColourB = new Color(0f, 0.05f, 0.3f, 1f); // dark blue

    [Header("Panic — pulses between yellow and orange")]
    [SerializeField] private Sprite _panicSprite;
    [SerializeField] private Color _panicColourA = new Color(1f, 0.9f, 0f, 1f);   // yellow
    [SerializeField] private Color _panicColourB = new Color(1f, 0.4f, 0f, 1f);   // orange

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

    public void ShowIkariRage() => _ikariMark?.Play(_rageSprite, _rageColourA, _rageColourB);
    public void ShowIkariFear() => _ikariMark?.Play(_fearSprite, _fearColourA, _fearColourB);
    public void ShowIkariPanic() => _ikariMark?.Play(_panicSprite, _panicColourA, _panicColourB);
    public void ShowIkari() => ShowIkariRage(); // default

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