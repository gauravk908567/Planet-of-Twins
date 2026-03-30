using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Accord State X bar UI.
///
/// UNITY SETUP:
///   AccordBarPanel  (root — hidden until Accord unlocked)
///     ├── BarSlider       Slider component — fill goes sky blue → violet
///     ├── FullPulseImage  Image (Knob sprite, Simple) — appears below slider when full/active
///     ├── HoldXText       TMP_Text — appears next to bar when full/active
///     └── ChargeRing      Image (Knob, Filled, Radial360, FillOrigin=Top) — charge progress
///
/// STATES:
///   Filling  — slider fills, colour lerps blue→violet, pulse/text hidden, ring hidden
///   Full     — slider violet, pulse image + HoldX text appear and flicker, ring hidden
///   Charging — ring fills as X is held, pulse/text keep flickering
///   Active   — slider depletes empty←full, pulse/text stay, ring hidden
///   Done     — bar resets to 0, pulse/text hide, starts filling again
/// </summary>
public class AccordBarView : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private AccordStateSystem accordSystem;
    [SerializeField] private MonoBehaviour unlockStateMono;

    [Header("Root — hidden until unlocked")]
    [SerializeField] private GameObject panel;

    [Header("Bar slider")]
    [SerializeField] private Slider barSlider;
    [SerializeField] private Image barFill;
    [SerializeField] private Color colourEmpty = new Color(0.25f, 0.75f, 1f, 1f); // sky blue
    [SerializeField] private Color colourFull = new Color(0.55f, 0.15f, 1f, 1f); // violet

    [Header("Full pulse — image below slider")]
    [SerializeField] private Image fullPulseImage;
    [SerializeField] private Color pulseColour = new Color(0.55f, 0.15f, 1f, 1f);
    [SerializeField] private float pulseSpeed = 3f;

    [Header("Hold X text — next to bar")]
    [SerializeField] private TMP_Text holdXText;

    [Header("Charge ring — radial fill while holding X")]
    [SerializeField] private Image chargeRing;
    [SerializeField] private Color chargeRingColour = new Color(1f, 0.9f, 0.3f, 1f); // gold

    // ── Runtime ───────────────────────────────────────────────
    private ISkillUnlockState _unlockState;
    private bool _isUnlocked = false;
    private float _pulseTimer = 0f;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Start()
    {
        _unlockState = unlockStateMono as ISkillUnlockState;

        Debug.Log($"[AccordBarView] accordSystem={accordSystem != null} " +
                  $"_unlockState={_unlockState != null} " +
                  $"panel={panel != null}");

        if (_unlockState != null)
            _unlockState.OnAccordStateUnlocked += HandleUnlocked;

        bool unlocked = _unlockState != null && _unlockState.IsAccordStateUnlocked;
        SetUnlocked(unlocked);
    }

    private void OnDestroy()
    {
        if (_unlockState != null)
            _unlockState.OnAccordStateUnlocked -= HandleUnlocked;
    }

    private void HandleUnlocked() => SetUnlocked(true);

    private void SetUnlocked(bool unlocked)
    {
        _isUnlocked = unlocked;
        panel?.SetActive(unlocked);
    }

    // ── Update ────────────────────────────────────────────────
    private void Update()
    {
        if (!_isUnlocked || accordSystem == null) return;

        _pulseTimer += Time.deltaTime * pulseSpeed;

        RefreshBar();
        RefreshPulseAndText();
        RefreshChargeRing();
    }

    // ── Bar slider ────────────────────────────────────────────
    private void RefreshBar()
    {
        if (barSlider == null) return;

        float displayValue;
        float colourT;

        if (accordSystem.IsAccordActive)
        {
            // Deplete from 1 → 0 over active duration
            displayValue = 1f - accordSystem.ActiveProgress;
            colourT = 1f; // stays violet during active
        }
        else
        {
            // Fill from 0 → 1 as bar points accumulate
            displayValue = accordSystem.BarProgress;
            colourT = accordSystem.BarProgress;
        }

        barSlider.value = displayValue;

        if (barFill != null)
            barFill.color = Color.Lerp(colourEmpty, colourFull, colourT);
    }

    // ── Pulse image + Hold X text ─────────────────────────────
    private void RefreshPulseAndText()
    {
        // Show when bar is full OR accord is active (stays during depletion)
        bool shouldShow = accordSystem.BarIsFull || accordSystem.IsAccordActive;

        if (fullPulseImage != null)
        {
            fullPulseImage.gameObject.SetActive(shouldShow);
            if (shouldShow)
            {
                // Flicker: oscillate alpha between 0.3 and 1.0
                float alpha = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(_pulseTimer) + 1f) * 0.5f);
                fullPulseImage.color = new Color(
                    pulseColour.r, pulseColour.g, pulseColour.b, alpha);
            }
        }

        if (holdXText != null)
        {
            holdXText.gameObject.SetActive(shouldShow);
            if (shouldShow)
            {
                // Same flicker as image — in sync
                float alpha = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(_pulseTimer) + 1f) * 0.5f);
                holdXText.alpha = alpha;
            }
        }
    }

    // ── Charge ring ───────────────────────────────────────────
    private void RefreshChargeRing()
    {
        if (chargeRing == null) return;

        // Show only while X is being held to charge (charge progress > 0, not yet active)
        bool charging = !accordSystem.IsAccordActive && accordSystem.ChargeProgress > 0f;
        chargeRing.gameObject.SetActive(charging);

        if (charging)
        {
            chargeRing.fillAmount = accordSystem.ChargeProgress;
            chargeRing.color = chargeRingColour;
        }
    }
}