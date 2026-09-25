using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Drives the overview camera button indicator text.
///
/// Uses two overlapping TMP_Text objects at the same position:
///   - Bottom (grey) — always visible, full width
///   - Top (orange) — same text, clipped by a RectMask2D parent whose
///     width animates from 0 (cooldown start) to full (cooldown end)
///
/// HIERARCHY SETUP:
///   OverviewHUDRoot (RectTransform, no mask)
///     ├── GreyText        (TMP_Text — grey, full width always)
///     └── OrangeMask      (RectTransform + RectMask2D — width animated)
///           └── OrangeText (TMP_Text — orange, same text, same position)
///
/// SETUP:
///   - Add this script to any always-active GO.
///   - Drag OverviewCamController into overviewController slot.
///   - Drag GreyText → greyText, OrangeText → orangeText, OrangeMask → orangeMask.
///   - Both texts set to same font, size, position. OrangeText pivot X = 0.
///   - OrangeMask starts at width 0.
/// </summary>
public class OverviewCamHUDView : MonoBehaviour
{
    [SerializeField] private OverviewCamController overviewController;

    [Header("UI References")]
    [SerializeField] private TMP_Text greyText;         // always visible, grey
    [SerializeField] private TMP_Text orangeText;       // clipped by mask
    [SerializeField] private RectTransform orangeMask;  // RectMask2D parent, width animated

    [Header("Settings")]
    [SerializeField] private string buttonLabel = "B";
    [SerializeField] private Color greyColour = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color activeColour = new Color(1f, 0.6f, 0.1f, 1f); // orange
    [SerializeField] private Color cooldownFillColour = new Color(1f, 0.6f, 0.1f, 1f); // same orange fill

    private float _fullWidth;
    private float _cooldownDuration;
    private float _cooldownRemaining;
    private bool _isActive;
    private bool _isCoolingDown;

    private void Awake()
    {
        if (greyText != null) greyText.text = buttonLabel;
        if (orangeText != null) orangeText.text = buttonLabel;
        if (greyText != null) greyText.color = greyColour;
        if (orangeText != null) orangeText.color = cooldownFillColour;

        _cooldownDuration = overviewController != null
            ? GetCooldownDuration() : 4f;
    }

    private System.Collections.IEnumerator Start()
    {
        overviewController ??= OverviewCamController.Instance;
        if (overviewController == null)
            Debug.LogError("[OverviewCamHUDView] OverviewCamController unresolved — is Persistent loaded?", this);
        else
        {
            _cooldownDuration = GetCooldownDuration();
            // Re-subscribe in case OnEnable fired before overviewController was resolved.
            overviewController.OnOverviewToggled -= HandleOverviewToggled;
            overviewController.OnOverviewToggled += HandleOverviewToggled;
        }

        // Wait one frame so Unity layout has calculated RectTransform sizes.
        // Capturing rect.width in Awake returns 0 before layout runs.
        yield return null;

        if (orangeMask != null)
        {
            _fullWidth = orangeMask.rect.width;
            // Start fully filled — camera available, no cooldown yet
            SetMaskWidth(_fullWidth);
            if (greyText != null) greyText.color = activeColour;
        }
    }

    private void OnEnable()
    {
        if (overviewController != null)
            overviewController.OnOverviewToggled += HandleOverviewToggled;
    }

    private void OnDisable()
    {
        if (overviewController != null)
            overviewController.OnOverviewToggled -= HandleOverviewToggled;
    }

    private void HandleOverviewToggled(bool active)
    {
        _isActive = active;

        if (active)
        {
            // Camera is active — show orange colour on text
            if (greyText != null) greyText.color = activeColour;
            if (orangeText != null) orangeText.color = activeColour;
            SetMaskWidth(_fullWidth);
            _isCoolingDown = false;
        }
        else
        {
            // Camera ended — start cooldown fill sweep
            if (greyText != null) greyText.color = greyColour;
            _cooldownRemaining = _cooldownDuration;
            _isCoolingDown = true;
            SetMaskWidth(0f); // start empty, fill right as cooldown completes
        }
    }

    private void Update()
    {
        if (!_isCoolingDown) return;

        _cooldownRemaining -= Time.deltaTime;

        float progress = 1f - Mathf.Clamp01(_cooldownRemaining / _cooldownDuration);
        SetMaskWidth(_fullWidth * progress);

        if (_cooldownRemaining <= 0f)
        {
            _isCoolingDown = false;
            SetMaskWidth(_fullWidth);
            // Cooldown done — restore orange/ready look
            if (greyText != null) greyText.color = activeColour;
        }
    }

    private void SetMaskWidth(float width)
    {
        if (orangeMask == null) return;
        var size = orangeMask.sizeDelta;
        size.x = width;
        orangeMask.sizeDelta = size;
    }

    // Reflection-free way to get cooldown — OverviewCamController exposes it publicly
    private float GetCooldownDuration()
    {
        // OverviewCamController.cooldownDuration is serialized private.
        // We hardcode the default here and let the designer match it in both Inspectors.
        // If you want to expose it, add: public float CooldownDuration => cooldownDuration;
        return overviewController.CooldownDuration;
    }
}