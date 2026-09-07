using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityIconUI : MonoBehaviour
{
    [Header("Children")]
    [SerializeField] private Image iconBG;
    [SerializeField] private Image cooldownRing;
    [SerializeField] private Image lockedOverlay;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text chargeText;
    [Tooltip("The dedicated 'which button to press' label (a static designer letter like C/Q). The button-glyph " +
             "system paints a device-aware input glyph here. Optional; auto-resolved from a direct child named " +
             "\"ButtonText\" in Awake when left unwired, so no per-instance wiring is needed.")]
    [SerializeField] private TMP_Text buttonText;

    [Tooltip("Border-as-timer (Track D): the clan-border/liquid-tank driver on the card Image. Optional; " +
             "auto-resolved from a child in Awake. When present, Bind()/Unbind() forward the ability source " +
             "to it so the border carries the cooldown/active/hold timer.")]
    [SerializeField] private BorderFillDriver borderDriver;

    [Header("Colours")]
    [SerializeField] private Color readyColour = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color activeColour = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.3f, 0.3f, 0.3f, 1f);

    private IAbilityHUDSource _source;
    private bool _isUnlocked = true;

    /// <summary>The ability-name label. (Button-glyph P2b interim used this as the glyph target; the P2b REFINE
    /// moved the glyph onto <see cref="ButtonLabel"/>, so this stays the ability name — Bind() sets it when the
    /// source names the ability, otherwise the designer's scene text shows.) May be null.</summary>
    public TMP_Text KeyLabel => nameText;

    /// <summary>Button-glyph system (P2b refine) — the dedicated "which button" label (the ButtonText child).
    /// This is where the device-aware input glyph is painted (moved off NameText). May be null (a slot without
    /// the child); auto-resolved by name in Awake when the serialized slot is empty. Bind()/Refresh() never
    /// touch it, so a glyph set here persists.</summary>
    public TMP_Text ButtonLabel => buttonText;

    [Header("Lock settings")]
    [SerializeField] private bool _startLocked = false;

    private void Awake()
    {
        // Button-glyph system (P2b refine): resolve the ButtonText child by name when the serialized slot is
        // unwired, so the glyph target works across every ability-icon scene instance without per-instance wiring
        // (these icons are scene objects, not a shared prefab). Direct child per the authored hierarchy.
        if (buttonText == null)
        {
            var t = transform.Find("ButtonText");
            if (t != null) buttonText = t.GetComponent<TMP_Text>();
        }

        // Border-as-timer driver (Track D): resolve from a child when unwired, so the border works
        // across every scene-instance ability icon without per-instance wiring.
        if (borderDriver == null)
            borderDriver = GetComponentInChildren<BorderFillDriver>(true);

        if (_startLocked)
            SetUnlocked(false);
    }

    public void Bind(IAbilityHUDSource source)
    {
        _source = source;
        // Only overwrite if source provides a non-empty name.
        // Designer's TMP text in scene is preserved when name is empty.
        if (nameText != null && !string.IsNullOrEmpty(source?.AbilityName))
            nameText.text = source.AbilityName;
        borderDriver?.SetSource(source);
        Refresh();
    }

    public void Unbind()
    {
        _source = null;
        borderDriver?.SetSource(null);
        SetCooldownRing(1f);
        if (chargeText != null) chargeText.gameObject.SetActive(false);
    }

    public void SetUnlocked(bool unlocked)
    {
        _isUnlocked = unlocked;
        gameObject.SetActive(unlocked);
    }

    /// <summary>Track D: hide the legacy ability-tile visuals (the <c>iconBG</c> state square + the old radial
    /// <c>cooldownRing</c>) when this slot uses the new border card — the card carries state + timer now. The
    /// name label and the button glyph stay. Disables only the Image components, so child glyphs still render.</summary>
    public void HideLegacyVisuals()
    {
        if (iconBG != null) iconBG.enabled = false;
        if (cooldownRing != null) cooldownRing.enabled = false;
        // The panel's OWN background Image (a semi-transparent white rounded rect) sits in front of the card and
        // veils it — hide it so the border/tank/glow read crisply. The name + glyph are separate children, unaffected.
        var panelBg = GetComponent<Image>();
        if (panelBg != null) panelBg.enabled = false;
    }

    private void Update()
    {
        if (!_isUnlocked || _source == null) return;
        Refresh();
    }

    private void Refresh()
    {
        if (_source == null) return;

        float progress = _source.CooldownProgress;
        SetCooldownRing(progress);

        if (iconBG != null)
        {
            iconBG.color = _source.IsActive
                ? activeColour
                : progress < 1f ? cooldownColour : readyColour;
        }

        // Suppression overlay managed by AbilitySuppressionUI — don't force-hide it here
        // lockedOverlay is only shown by AbilitySuppressionUI when ability is suppressed

        if (chargeText != null)
        {
            bool showCharges = _source.MaxCharges > 1;
            chargeText.gameObject.SetActive(showCharges);
            if (showCharges)
                chargeText.text = $"{_source.CurrentCharges}/{_source.MaxCharges}";
        }
    }

    private void SetCooldownRing(float normalised)
    {
        if (cooldownRing != null)
            cooldownRing.fillAmount = Mathf.Clamp01(normalised);
    }
}