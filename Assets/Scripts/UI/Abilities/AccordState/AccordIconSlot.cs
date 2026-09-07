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

    [Header("Border-as-timer (Track D) — driver on the owner frame")]
    [Tooltip("Drives the clan border + liquid-tank timer on the owner-frame card (PoT/UIAbilityCard). " +
             "Auto-resolved from ownerFrameLeft in Awake when unwired. BindNormal forwards the ability source " +
             "to it so the border shows this slot's cooldown/active/hold. Null = slot not yet converted (no-op).")]
    [SerializeField] private BorderFillDriver borderDriver;

    [Header("Animation")]
    [SerializeField] private float slotHeight = 80f;
    [SerializeField] private float animDuration = 0.18f;

    // ── Runtime ───────────────────────────────────────────────
    private IAbilityActiveState _accordAbilityState;
    // The border card is a STATIC sibling of both panels (it doesn't slide), so when the slot swaps to Accord we
    // instead re-point the border driver from the normal-mode source onto the accord-mode source (e.g. the SC
    // card starts reading Setsuna). Both are cached here so the swap is a null-safe SetSource, no re-resolve.
    private IAbilityHUDSource _normalSource;
    private IAbilityHUDSource _accordSource;
    private bool _isInAccordMode = false;
    private Coroutine _animCoroutine;
    private Coroutine _vfxCoroutine;

    // Track D keycap style — a glowing clan LETTER (keyboard) / clan ICON (gamepad) on a soft clan keycap. The two
    // glow materials (gold/violet halo, white face) + the keycap base live in Resources/AbilityHUD, loaded once and
    // shared read-only; the keycap material is CLONED per slot so it can carry this slot's clan (single or dual).
    private static Material _matGoldGlow, _matVioletGlow, _matNeutralGlow, _matKeycapBase;
    private static bool _keyCapMatsLoaded;
    private Image _normalKeyCap, _accordKeyCap;   // the soft clan keycap built behind each key label
    private Material _normalKeyCapMat, _accordKeyCapMat;   // cached keycap clones, driven each frame for the corona
    private bool _keyCapStyled;
    private static readonly int ID_ActiveGlow = Shader.PropertyToID("_ActiveGlow");

    private void Awake()
    {
        // Border-as-timer (Track D): the driver lives on the owner-frame card (a sibling of the icon panels,
        // static on the slot). Resolve it from ownerFrameLeft when unwired so converted slots need no extra wiring.
        if (borderDriver == null && ownerFrameLeft != null)
            borderDriver = ownerFrameLeft.GetComponent<BorderFillDriver>();

        // When this slot uses the new border card: push the card BEHIND the icon panels so the glyph reads on top,
        // and hide the legacy ability tiles (iconBG + old cooldownRing) on both panels — the card is the visual now.
        if (borderDriver != null)
        {
            ownerFrameLeft.transform.SetAsFirstSibling();
            normalIconUI?.HideLegacyVisuals();
            accordIconUI?.HideLegacyVisuals();
        }
    }

    // ── Init ──────────────────────────────────────────────────
    public void BindNormal(IAbilityHUDSource source)
    {
        _normalSource = source;
        normalIconUI?.Bind(source);
        // Border follows the normal-mode ability by default (Track D); the accord swap re-points it when the
        // slot enters Accord. If the slot is already in Accord, keep showing the accord source (don't yank it).
        if (!_isInAccordMode) borderDriver?.SetSource(source);
    }

    public void BindAccord(IAbilityHUDSource source, IAbilityActiveState activeState)
    {
        _accordSource = source;
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
        EnsureKeyCapStyle();
        // Track D: keyboard → the bare key LETTER (the glowing hero); gamepad → the device ICON. No keycap box.
        InputGlyphText.ApplyKeyCap(NormalKeyLabel, provider, actionName);
        InputGlyphText.ApplyKeyCap(AccordKeyLabel, provider, actionName);
        // Colour: keyboard → the clan LETTER (glow-material face is white, so the vertex colour makes it clan + the
        // material adds the clan halo). Gamepad → the icon SPRITE kept BRIGHT/white so it pops off the clan keycap
        // (a clan-tinted icon on a clan keycap reads muddy); clan identity stays on the keycap + border.
        if (borderDriver != null)
        {
            bool pad = InputGlyphResolver.ResolveKind(provider) == InputDeviceKind.Gamepad;
            Color c = pad ? Color.white : borderDriver.GlyphColor;
            if (NormalKeyLabel != null) NormalKeyLabel.color = c;
            if (AccordKeyLabel != null) AccordKeyLabel.color = c;
        }
    }

    /// <summary>Joint ability: ONE glyph when both players share a device kind, else BOTH glyphs (kb + pad).</summary>
    public void ApplyKeyGlyphJoint(IInputProvider a, IInputProvider b, string actionName)
    {
        EnsureKeyCapStyle();
        // Per-side clan colour: left glyph = Lyra gold (GlyphColor), right glyph = Kai violet (GlyphColorRight).
        Color left = borderDriver != null ? borderDriver.GlyphColor : Color.white;
        Color right = borderDriver != null ? borderDriver.GlyphColorRight : Color.white;
        InputGlyphText.ApplyKeyCapJoint(NormalKeyLabel, a, b, actionName, left, right);
        InputGlyphText.ApplyKeyCapJoint(AccordKeyLabel, a, b, actionName, left, right);
    }

    // ── Track D keycap style (glowing clan letter/icon + soft clan keycap) ─────────────────────────────────────
    private static void EnsureKeyCapMaterials()
    {
        if (_keyCapMatsLoaded) return;
        _keyCapMatsLoaded = true;
        _matGoldGlow    = Resources.Load<Material>("AbilityHUD/M_UIKeyLetterGlow_Gold");
        _matVioletGlow  = Resources.Load<Material>("AbilityHUD/M_UIKeyLetterGlow_Violet");
        _matNeutralGlow = Resources.Load<Material>("AbilityHUD/M_UIKeyLetterGlow_Neutral");
        _matKeycapBase  = Resources.Load<Material>("AbilityHUD/M_UIKeycap");
        if (_matGoldGlow == null || _matVioletGlow == null || _matKeycapBase == null)
            Debug.LogError("[AccordIconSlot] Missing keycap materials under Resources/AbilityHUD — glowing-letter " +
                           "keycaps fall back to plain labels.");
    }

    /// <summary>Apply the glowing-letter keycap style to both panels' key labels ONCE: assign the clan glow material
    /// (gold for a Lyra-owned slot, violet for a Kai-owned slot), opt the label OUT of the panel mask so its glow
    /// halo isn't clipped, and build the soft clan keycap behind it. No-op without a border driver.</summary>
    private void EnsureKeyCapStyle()
    {
        if (_keyCapStyled || borderDriver == null) return;
        _keyCapStyled = true;
        EnsureKeyCapMaterials();
        // Halo colour by owner: a JOINT (dual) slot is owned by neither → a NEUTRAL cool-white halo (the split
        // gold/violet identity stays on the border + keycap); a single-owner slot takes its clan — violet has more
        // blue than red, gold the reverse. Per-side face colours for a split-device joint are set in ApplyKeyCapJoint.
        Material glow = borderDriver.IsDual ? (_matNeutralGlow != null ? _matNeutralGlow : _matGoldGlow)
                      : borderDriver.ClanA.b > borderDriver.ClanA.r ? _matVioletGlow : _matGoldGlow;
        StyleKeyLabel(NormalKeyLabel, glow, ref _normalKeyCap);
        StyleKeyLabel(AccordKeyLabel, glow, ref _accordKeyCap);
        // Cache the per-slot keycap clones so Update() can drive their active corona each frame.
        _normalKeyCapMat = _normalKeyCap != null ? _normalKeyCap.material : null;
        _accordKeyCapMat = _accordKeyCap != null ? _accordKeyCap.material : null;

        // Only the active-mode panel's glyph may show. At style time the slot is in normal mode, so this hides the
        // accord glyph — otherwise it leaks past the panel mask while the accord ability is still off-panel.
        RefreshGlyphVisibility();
    }

    private void Update()
    {
        // Drive the "solar eclipse" active corona on the keycap(s) from the border driver's ActiveGlow envelope
        // (1 while the ability is firing, fading after). Cheap: two SetFloats on the visible slot's keycap.
        if (borderDriver == null) return;
        float g = borderDriver.ActiveGlow;
        if (_normalKeyCapMat != null) _normalKeyCapMat.SetFloat(ID_ActiveGlow, g);
        if (_accordKeyCapMat != null) _accordKeyCapMat.SetFloat(ID_ActiveGlow, g);
    }

    private void StyleKeyLabel(TMP_Text label, Material glow, ref Image keyCap)
    {
        if (label == null) return;
        // The panel is masked → a masked label renders through a stencil variant, so our glow material would never
        // reach the drawn pixels (the same trap the border card hit). Opt out so it draws with its own glow material.
        label.maskable = false;
        if (glow != null) label.fontSharedMaterial = glow;
        if (keyCap == null) keyCap = BuildKeyCap(label);
    }

    /// <summary>Build the soft, rounded, faint-clan keycap that sits BEHIND <paramref name="label"/> — a cloned
    /// PoT/UIAbilityCard with border/tank off, tinted this slot's clan. Non-maskable so its soft edge isn't clipped.</summary>
    private Image BuildKeyCap(TMP_Text label)
    {
        var parent = label.rectTransform.parent as RectTransform;
        if (parent == null) return null;

        var go = new GameObject("KeyCap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = label.rectTransform.anchorMin;
        rt.anchorMax = label.rectTransform.anchorMax;
        rt.pivot = label.rectTransform.pivot;
        rt.anchoredPosition = label.rectTransform.anchoredPosition;
        Vector2 sz = label.rectTransform.rect.size;
        // Quad is bigger than the visible cap (which the material's _Margin insets) so the ACTIVE corona has room to
        // radiate OUTWARD beyond the cap edge instead of being clipped at the quad boundary.
        rt.sizeDelta = new Vector2(Mathf.Max(sz.x, 40f) + 30f, Mathf.Max(sz.y, 40f) + 30f);

        var img = go.GetComponent<Image>();
        img.maskable = false;
        img.raycastTarget = false;
        if (_matKeycapBase != null)
        {
            var m = new Material(_matKeycapBase);   // per-slot clone so it can carry this slot's clan
            m.SetColor("_ClanA", borderDriver.ClanA);
            m.SetColor("_ClanB", borderDriver.ClanB);
            m.SetFloat("_DualClan", borderDriver.IsDual ? 1f : 0f);
            m.SetFloat("_Margin", 0.16f);   // inset the visible cap so the enlarged quad leaves an outward-glow margin
            m.SetFloat("_Aspect", rt.sizeDelta.x / Mathf.Max(rt.sizeDelta.y, 0.0001f));   // keep the rounded corners round
            img.material = m;
        }
        rt.SetSiblingIndex(label.rectTransform.GetSiblingIndex());   // render BEHIND the label
        return img;
    }

    /// <summary>Passive ability (no keybind — e.g. Coalesce auto-triggers) → clear the key-cap.</summary>
    public void ClearKeyGlyph()
    {
        if (NormalKeyLabel != null) NormalKeyLabel.text = "";
        if (AccordKeyLabel != null) AccordKeyLabel.text = "";
    }

    // ── Button-glyph visibility ───────────────────────────────
    // The keycap letter/icon and its soft cap are opted OUT of the panel Mask (so the glow halo isn't clipped),
    // which also means an off-panel form's glyph would LEAK straight through the mask. So the glyph of the form
    // that is NOT currently on-panel must be hidden by hand — only the active-mode panel shows its trigger glyph.
    // Fix for "the button to trigger an accord ability shows even while that ability is hidden behind the mask".
    private void SetGlyphVisible(TMP_Text label, Image keyCap, bool visible)
    {
        if (label != null) label.enabled = visible;
        if (keyCap != null) keyCap.enabled = visible;
    }

    /// <summary>Settle glyph visibility to the current mode: the normal glyph shows only in normal mode AND only
    /// when the normal panel is itself visible (unlocked); the accord glyph shows only in accord mode.</summary>
    private void RefreshGlyphVisibility()
    {
        bool normalVisible = !_isInAccordMode && normalIconUI != null && normalIconUI.gameObject.activeSelf;
        SetGlyphVisible(NormalKeyLabel, _normalKeyCap, normalVisible);
        SetGlyphVisible(AccordKeyLabel, _accordKeyCap, _isInAccordMode);
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

        // Hard reset (not mid-accord) — make sure the border reads the normal-mode ability.
        if (_normalSource != null) borderDriver?.SetSource(_normalSource);

        _isInAccordMode = false;

        // Settle glyph visibility to normal mode (accord glyph hidden — its ability is off-panel).
        RefreshGlyphVisibility();
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

        // The card doesn't slide with the panels — swap the border driver onto the accord ability's source so it
        // now reads the accord form (e.g. the SC card shows Setsuna). No-op when the slot has no accord source.
        if (_accordSource != null) borderDriver?.SetSource(_accordSource);

        // Glyphs are non-maskable, so a sliding panel's glyph would leak past the mask. Hide BOTH for the slide;
        // the accord glyph is revealed once it has settled on-panel (at the end of this coroutine).
        SetGlyphVisible(NormalKeyLabel, _normalKeyCap, false);
        SetGlyphVisible(AccordKeyLabel, _accordKeyCap, false);

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

        // Accord ability is now on-panel → its trigger glyph may show.
        SetGlyphVisible(AccordKeyLabel, _accordKeyCap, true);

        if (_vfxCoroutine != null) StopCoroutine(_vfxCoroutine);
        _vfxCoroutine = StartCoroutine(PulseVFX());
    }

    private IEnumerator SlideToNormal()
    {
        if (_vfxCoroutine != null) { StopCoroutine(_vfxCoroutine); _vfxCoroutine = null; }
        if (accordVFXOverlay != null) accordVFXOverlay.color = vfxColourMin;

        // Returning to normal — point the border driver back at the normal-mode ability (the deferred-return path
        // has already waited out the accord ability's active window, so the card doesn't snap mid-drain).
        if (_normalSource != null) borderDriver?.SetSource(_normalSource);

        // Hide both glyphs during the slide (non-maskable → would leak); the normal glyph is restored at the end.
        SetGlyphVisible(NormalKeyLabel, _normalKeyCap, false);
        SetGlyphVisible(AccordKeyLabel, _accordKeyCap, false);

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

        // Normal ability back on-panel → show its glyph (only when the normal panel itself is visible/unlocked).
        SetGlyphVisible(NormalKeyLabel, _normalKeyCap, normalVisible);
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