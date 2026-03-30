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

    [Header("Colours")]
    [SerializeField] private Color readyColour = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color activeColour = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.3f, 0.3f, 0.3f, 1f);

    private IAbilityHUDSource _source;
    private bool _isUnlocked = true;

    [Header("Lock settings")]
    [SerializeField] private bool _startLocked = false;

    private void Awake()
    {
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
        Refresh();
    }

    public void Unbind()
    {
        _source = null;
        SetCooldownRing(1f);
        if (chargeText != null) chargeText.gameObject.SetActive(false);
    }

    public void SetUnlocked(bool unlocked)
    {
        _isUnlocked = unlocked;
        gameObject.SetActive(unlocked);
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

        if (lockedOverlay != null)
            lockedOverlay.gameObject.SetActive(false);

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