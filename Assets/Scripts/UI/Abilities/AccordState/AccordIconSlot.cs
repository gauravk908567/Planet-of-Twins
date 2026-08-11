using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One ability slot in the HUD that supports Accord State swapping.
///
/// SCENE SETUP per slot:
///   AccordIconSlot (this script + RectTransform)
///     ├── NormalPanel   — existing AbilityIconUI prefab instance
///     └── AccordPanel   — existing AbilityIconUI prefab instance
///           └── AccordVFXOverlay  — Image, soft glow circle, child of AccordPanel root
///
/// Both panels share the same anchor/pivot. NormalPanel is at y=0.
/// On accord activate: NormalPanel exits downward, AccordPanel falls from above.
/// On accord deactivate: reverse — deferred if accord ability is still active.
///
/// AccordVFXOverlay pulses alpha while accord is active to give the "super bar" glow.
/// </summary>
public class AccordIconSlot : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private RectTransform normalPanel;
    [SerializeField] private RectTransform accordPanel;

    [Header("Ability icon UI references")]
    [SerializeField] private AbilityIconUI normalIconUI;
    [SerializeField] private AbilityIconUI accordIconUI;

    [Header("Accord VFX overlay — Image child of AccordPanel")]
    [SerializeField] private Image accordVFXOverlay;
    [SerializeField] private Color vfxColourMin = new Color(0.5f, 0.3f, 1f, 0f);
    [SerializeField] private Color vfxColourMax = new Color(0.5f, 0.3f, 1f, 0.6f);
    [SerializeField] private float vfxPulseSpeed = 1.8f;

    [Header("Animation")]
    [SerializeField] private float slotHeight = 80f;
    [SerializeField] private float animDuration = 0.18f;

    // ── Runtime ───────────────────────────────────────────────
    private IAbilityActiveState _accordAbilityState;
    private bool _isInAccordMode = false;
    private Coroutine _animCoroutine;
    private Coroutine _vfxCoroutine;

    // ── Init ──────────────────────────────────────────────────
    public void BindNormal(IAbilityHUDSource source)
    {
        normalIconUI?.Bind(source);
    }

    public void BindAccord(IAbilityHUDSource source, IAbilityActiveState activeState)
    {
        accordIconUI?.Bind(source);
        _accordAbilityState = activeState;
    }

    public void SetNormalUnlocked(bool unlocked)
    {
        normalIconUI?.SetUnlocked(unlocked);
    }

    public void SetAccordUnlocked(bool unlocked)
    {
        accordIconUI?.SetUnlocked(unlocked);
    }

    // ── Called by AccordHUDController ─────────────────────────
    public void AnimateToAccord(float delay)
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateWithDelay(delay, toAccord: true));
    }

    public void AnimateToNormal()
    {
        if (!_isInAccordMode)
        {
            ForceResetToNormal();
            return;
        }

        if (_accordAbilityState != null && _accordAbilityState.IsAbilityActive)
        {
            StartCoroutine(DeferredReturnToNormal());
            return;
        }

        DoReturnToNormal();
    }

    public void ForceResetToNormal()
    {
        if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
        if (_vfxCoroutine != null) { StopCoroutine(_vfxCoroutine); _vfxCoroutine = null; }

        if (accordVFXOverlay != null) accordVFXOverlay.color = vfxColourMin;

        if (normalPanel != null)
        {
            bool normalVisible = normalIconUI != null && normalIconUI.gameObject.activeSelf;
            normalPanel.anchoredPosition = normalVisible
                ? Vector2.zero
                : new Vector2(0f, -slotHeight);
        }

        if (accordPanel != null)
            accordPanel.anchoredPosition = new Vector2(0f, slotHeight);

        _isInAccordMode = false;
    }

    // ── Animation coroutines ──────────────────────────────────
    private IEnumerator AnimateWithDelay(float delay, bool toAccord)
    {
        yield return new WaitForSeconds(delay);

        if (toAccord)
            yield return StartCoroutine(SlideToAccord());
        else
            yield return StartCoroutine(SlideToNormal());
    }

    private IEnumerator SlideToAccord()
    {
        _isInAccordMode = true;

        accordPanel.anchoredPosition = new Vector2(0f, slotHeight);
        normalPanel.anchoredPosition = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            float t = elapsed / animDuration;
            float ease = EaseOutCubic(t);

            normalPanel.anchoredPosition = new Vector2(0f, -slotHeight * ease);
            accordPanel.anchoredPosition = new Vector2(0f, slotHeight * (1f - ease));

            elapsed += Time.deltaTime;
            yield return null;
        }

        normalPanel.anchoredPosition = new Vector2(0f, -slotHeight);
        accordPanel.anchoredPosition = Vector2.zero;

        if (_vfxCoroutine != null) StopCoroutine(_vfxCoroutine);
        _vfxCoroutine = StartCoroutine(PulseVFX());
    }

    private IEnumerator SlideToNormal()
    {
        if (_vfxCoroutine != null) { StopCoroutine(_vfxCoroutine); _vfxCoroutine = null; }
        if (accordVFXOverlay != null) accordVFXOverlay.color = vfxColourMin;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            float t = elapsed / animDuration;
            float ease = EaseOutCubic(t);

            accordPanel.anchoredPosition = new Vector2(0f, -slotHeight * ease);
            normalPanel.anchoredPosition = new Vector2(0f, slotHeight * (1f - ease));

            elapsed += Time.deltaTime;
            yield return null;
        }

        accordPanel.anchoredPosition = new Vector2(0f, -slotHeight);

        bool normalVisible = normalIconUI != null && normalIconUI.gameObject.activeSelf;
        normalPanel.anchoredPosition = normalVisible
            ? Vector2.zero
            : new Vector2(0f, -slotHeight);

        _isInAccordMode = false;
    }

    private IEnumerator DeferredReturnToNormal()
    {
        yield return new WaitUntil(() =>
            _accordAbilityState == null || !_accordAbilityState.IsAbilityActive);

        DoReturnToNormal();
    }

    private void DoReturnToNormal()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(SlideToNormal());
    }

    // ── VFX pulse ─────────────────────────────────────────────
    private IEnumerator PulseVFX()
    {
        if (accordVFXOverlay == null) yield break;

        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * vfxPulseSpeed;
            float alpha = (Mathf.Sin(t) + 1f) * 0.5f;
            accordVFXOverlay.color = Color.Lerp(vfxColourMin, vfxColourMax, alpha);
            yield return null;
        }
    }

    // ── Easing ────────────────────────────────────────────────
    private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
}