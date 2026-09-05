using System.Collections;
using TMPro;
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

    [Header("Owner tint — clan identity frame (couch M5)")]
    [Tooltip("Left/right halves of the slot's owner frame — the couch co-op ownership signal. " +
             "Single-owner slot: SetOwnerTint paints both halves the clan colour (a solid frame). " +
             "Joint slot (SC/Coalesce/Empower): left = Lyra gold, right = Kai violet (a split frame). " +
             "Lives on the SLOT (static — never slides with the accord panels); the cooldown ring stays " +
             "STATE-only, so identity and cooldown never share a channel. Either image may be left null.")]
    [SerializeField] private Image ownerFrameLeft;
    [SerializeField] private Image ownerFrameRight;

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

    /// <summary>Couch M5 — paint the slot's clan-owner frame. Same colour twice = a solid frame (one twin owns
    /// this slot); gold + violet = a split frame for a JOINT power (SC/Coalesce/Empower — owned by neither).
    /// Only the HUE is driven — each frame's own ALPHA is preserved, so the frame's opacity/shape stays a
    /// scene + UI-shader concern (a dedicated border-frame material can own the look; this only says "whose").
    /// Null frame images are skipped, so a slot without a frame is a no-op.</summary>
    public void SetOwnerTint(Color left, Color right)
    {
        if (ownerFrameLeft != null)
            ownerFrameLeft.color = new Color(left.r, left.g, left.b, ownerFrameLeft.color.a);
        if (ownerFrameRight != null)
            ownerFrameRight.color = new Color(right.r, right.g, right.b, ownerFrameRight.color.a);
    }

    // ── Button-glyph key-cap (P2b refine) ─────────────────────
    // Paint BOTH panels' ButtonLabel (AbilityIconUI's dedicated `ButtonText` child) with a device-aware input
    // glyph. The accord ability is triggered by the SAME button as its normal form (Possess/RadiantSeeker →
    // Ability, gate → Teleport, …) so the same action glyph is correct on the accord panel too. REFINE: the glyph
    // moved OFF NameText onto ButtonText — NameText now reverts to the ability name, ButtonText shows the button
    // glyph (was a static designer letter). Null-safe when a panel has no ButtonText child.
    private TMP_Text NormalKeyLabel => normalIconUI != null ? normalIconUI.ButtonLabel : null;
    private TMP_Text AccordKeyLabel => accordIconUI != null ? accordIconUI.ButtonLabel : null;

    /// <summary>Single-owner ability: paint the key-cap with the owner's device glyph for
    /// <paramref name="actionName"/> (follows the paired/last-used device, Overwatch-style).</summary>
    public void ApplyKeyGlyph(IInputProvider provider, string actionName)
    {
        string template = "{" + actionName + "}";
        InputGlyphText.Apply(NormalKeyLabel, template, provider);
        InputGlyphText.Apply(AccordKeyLabel, template, provider);
    }

    /// <summary>Joint ability: ONE glyph when both players share a device kind, else BOTH glyphs (kb + pad).</summary>
    public void ApplyKeyGlyphJoint(IInputProvider a, IInputProvider b, string actionName)
    {
        InputGlyphText.ApplyJoint(NormalKeyLabel, a, b, actionName);
        InputGlyphText.ApplyJoint(AccordKeyLabel, a, b, actionName);
    }

    /// <summary>Passive ability (no keybind — e.g. Coalesce auto-triggers) → clear the key-cap.</summary>
    public void ClearKeyGlyph()
    {
        if (NormalKeyLabel != null) NormalKeyLabel.text = "";
        if (AccordKeyLabel != null) AccordKeyLabel.text = "";
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