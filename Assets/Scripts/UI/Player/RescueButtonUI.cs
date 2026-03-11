// Shows the F-button mash prompt with:
//   • Fill bar (mash progress 0→1)
//   • Countdown ring (time remaining in mash window)
//   • Cooldown overlay (0.75s cooldown state)
//   • Key label "F"
//
// Attach to: HUD Canvas > RescuePanel
// Wiring:
//   rescueEventController → PlayerManager > RescueEventController
//   fillBar               → Slider component for mash progress
//   timerImage            → Image (radial fill type) for countdown ring
//   cooldownOverlay       → Panel that shows during cooldown
//   rootPanel             → parent panel to show/hide entire UI
// ───────────────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RescueButtonUI : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private RescueEventController rescueEventController;

    [Header("UI Elements")]
    [SerializeField] private GameObject rootPanel;       // parent — hidden when idle
    [SerializeField] private Slider fillBar;         // mash progress
    [SerializeField] private Image timerRing;       // Image type = Filled, radial
    [SerializeField] private GameObject cooldownOverlay; // shown during cooldown state
    [SerializeField] private TMP_Text cooldownText;    // "0.75" countdown
    [SerializeField] private TMP_Text instructionText; // "Press F repeatedly!"
    [SerializeField] private Image keyIcon;         // optional F-key icon

    [Header("Colours")]
    [SerializeField] private Color activeColour = new Color(0.2f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color dangerColour = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.5f, 0.5f, 0.5f, 1f);

    private void OnEnable()
    {
        rescueEventController.OnRescueStateChanged += HandleStateChanged;
        rescueEventController.OnMashProgressUpdated += HandleMashProgress;
        rescueEventController.OnMashTimeUpdated += HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated += HandleCooldownTime;
    }

    private void OnDisable()
    {
        rescueEventController.OnRescueStateChanged -= HandleStateChanged;
        rescueEventController.OnMashProgressUpdated -= HandleMashProgress;
        rescueEventController.OnMashTimeUpdated -= HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated -= HandleCooldownTime;
    }

    private void HandleStateChanged(RescueState state)
    {
        switch (state)
        {
            case RescueState.Idle:
            case RescueState.Failed:
                rootPanel?.SetActive(false);
                break;

            case RescueState.Triggered:
                rootPanel?.SetActive(true);
                cooldownOverlay?.SetActive(false);
                if (fillBar) fillBar.value = 0f;
                if (timerRing) timerRing.fillAmount = 1f;
                if (timerRing) timerRing.color = activeColour;
                if (instructionText) instructionText.text = "Press F to rescue!";
                break;

            case RescueState.Mashing:
                cooldownOverlay?.SetActive(false);
                if (timerRing) timerRing.color = activeColour;
                break;

            case RescueState.Cooldown:
                cooldownOverlay?.SetActive(true);
                if (timerRing) timerRing.color = cooldownColour;
                if (instructionText) instructionText.text = "Wait...";
                break;

            case RescueState.Success:
                rootPanel?.SetActive(false);
                break;
            case RescueState.SoulDied:
                rootPanel?.SetActive(true);
                cooldownOverlay?.SetActive(false);
                if (fillBar) fillBar.value = 0f;
                if (timerRing) timerRing.fillAmount = 1f;
                if (timerRing) timerRing.color = cooldownColour;
                if (instructionText) instructionText.text = "Soul destroyed — recharging...";
                break;
        }
    }

    private void HandleMashProgress(float normalised)
    {
        if (fillBar == null) return;
        fillBar.value = normalised;

        // Colour shifts to danger as bar fills (urgency feedback)
        if (timerRing)
            timerRing.color = Color.Lerp(activeColour, dangerColour, normalised);
    }

    private void HandleMashTime(float secondsRemaining)
    {
        if (timerRing == null) return;

        // timerRing must be set to Image type = Filled, Fill Method = Radial 360
        // This shrinks the ring clockwise as time runs out
        float total = rescueEventController != null ? GetMashWindowDuration() : 3f;
        timerRing.fillAmount = secondsRemaining / total;

        // Turn red when less than 1 second left
        if (secondsRemaining < 1f && timerRing)
            timerRing.color = dangerColour;
    }

    private void HandleCooldownTime(float secondsRemaining)
    {
        if (cooldownText)
            cooldownText.text = secondsRemaining.ToString("F1");
    }

    // Cached from controller — avoids repeated property access
    private float _cachedMashWindowDuration = -1f;
    private float GetMashWindowDuration()
    {
        return 3f; // default; update if you expose this from RescueEventController
    }
}
