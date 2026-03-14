using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One ability icon slot on the HUD.
/// Layout required (children of this RectTransform):
///   Image "IconBG"       — background tint (set color to ability colour)
///   Image "CooldownRing" — filled radial 360 image, overlaid on icon
///   Image "LockedOverlay"— dark semi-transparent overlay, shown when locked
///   TMP_Text "NameText"  — ability name (prototype label)
///   TMP_Text "ChargeText"— charge count, hidden when MaxCharges <= 1
///
/// Call Bind() from AbilityHUDController after the source is available.
/// Call Unbind() when tearing down.
/// Call SetUnlocked(false) for abilities that require unlock.
/// </summary>
public class AbilityIconUI : MonoBehaviour
{
    [Header("Children")]
    [SerializeField] private Image iconBG;
    [SerializeField] private Image cooldownRing;    // Image.Type = Filled, FillMethod = Radial360
    [SerializeField] private Image lockedOverlay;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text chargeText;

    [Header("Colours")]
    [SerializeField] private Color readyColour = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color activeColour = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.3f, 0.3f, 0.3f, 1f);

    private IAbilityHUDSource _source;
    private bool _isUnlocked = true;

    // ── Public API ────────────────────────────────────────────
    public void Bind(IAbilityHUDSource source)
    {
        _source = source;
        if (nameText != null)
            nameText.text = source?.AbilityName ?? "";
        Refresh();
    }

    public void Unbind()
    {
        _source = null;
        SetCooldownRing(1f);
        if (chargeText != null) chargeText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Set to false for abilities that must be purchased before appearing.
    /// Icon slot stays hidden until SetUnlocked(true) is called.
    /// </summary>
    public void SetUnlocked(bool unlocked)
    {
        _isUnlocked = unlocked;
        gameObject.SetActive(unlocked);
    }

    // ── Unity ─────────────────────────────────────────────────
    private void Update()
    {
        if (!_isUnlocked || _source == null) return;
        Refresh();
    }

    // ── Internal ──────────────────────────────────────────────
    private void Refresh()
    {
        if (_source == null) return;

        float progress = _source.CooldownProgress;
        SetCooldownRing(progress);

        // BG colour: active = green, on cooldown = dark, ready = normal
        if (iconBG != null)
        {
            iconBG.color = _source.IsActive
                ? activeColour
                : progress < 1f ? cooldownColour : readyColour;
        }

        // Locked overlay
        if (lockedOverlay != null)
            lockedOverlay.gameObject.SetActive(false); // unlocked icons never show overlay

        // Charge text
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