using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the rescue mash UI.
///
/// FIX: subscribes to OnActiveTargetChanged in addition to OnRescueStateChanged.
/// When a second player gets trapped immediately after a failed rescue, the state
/// briefly sits at Failed/Idle while _activeTarget is already set — the old code
/// hid on Failed and never re-showed until Triggered fired. Now we show as soon
/// as a non-null active target exists, before the soul reaches proximity.
/// </summary>
public class RescueButtonUI : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private RescueEventController rescueEventController;

    [Header("UI Elements")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Slider fillBar;
    [SerializeField] private Image timerRing;
    [SerializeField] private GameObject cooldownOverlay;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Image keyIcon;

    [Header("Colours")]
    [SerializeField] private Color activeColour = new Color(0.2f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color dangerColour = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.5f, 0.5f, 0.5f, 1f);

    private void Start()
    {
        rescueEventController ??= RescueEventController.Instance;
        if (rescueEventController == null) { Debug.LogError("[RescueButtonUI] RescueEventController not found.", this); enabled = false; return; }
        rescueEventController.OnRescueStateChanged -= HandleStateChanged;
        rescueEventController.OnRescueStateChanged += HandleStateChanged;
        rescueEventController.OnMashProgressUpdated -= HandleMashProgress;
        rescueEventController.OnMashProgressUpdated += HandleMashProgress;
        rescueEventController.OnMashTimeUpdated -= HandleMashTime;
        rescueEventController.OnMashTimeUpdated += HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated -= HandleCooldownTime;
        rescueEventController.OnCooldownTimeUpdated += HandleCooldownTime;
        rescueEventController.OnActiveTargetChanged -= HandleActiveTargetChanged;
        rescueEventController.OnActiveTargetChanged += HandleActiveTargetChanged;
    }

    private void OnEnable()
    {
        if (rescueEventController == null) return;
        rescueEventController.OnRescueStateChanged += HandleStateChanged;
        rescueEventController.OnMashProgressUpdated += HandleMashProgress;
        rescueEventController.OnMashTimeUpdated += HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated += HandleCooldownTime;
        rescueEventController.OnActiveTargetChanged += HandleActiveTargetChanged;
    }

    private void OnDisable()
    {
        if (rescueEventController == null) return;
        rescueEventController.OnRescueStateChanged -= HandleStateChanged;
        rescueEventController.OnMashProgressUpdated -= HandleMashProgress;
        rescueEventController.OnMashTimeUpdated -= HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated -= HandleCooldownTime;
        rescueEventController.OnActiveTargetChanged -= HandleActiveTargetChanged;
    }

    // FIX: show the panel as soon as any player is in danger, before soul arrives.
    // Without this, there's a window after a failed rescue where a second trap
    // grabs a player but the UI stays hidden until the state machine catches up.
    private void HandleActiveTargetChanged(IRescueTarget target)
    {
        if (target != null)
        {
            rootPanel?.SetActive(true);
            cooldownOverlay?.SetActive(false);
            if (fillBar) fillBar.value = 0f;
            if (timerRing) timerRing.fillAmount = 1f;
            if (timerRing) timerRing.color = activeColour;
            if (instructionText) instructionText.text = "Soul to rescue!";
        }
    }

    private void HandleStateChanged(RescueState state)
    {
        switch (state)
        {
            case RescueState.Idle:
                if (rescueEventController.ActiveTarget == null)
                    rootPanel?.SetActive(false);
                break;

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
        if (timerRing)
            timerRing.color = Color.Lerp(activeColour, dangerColour, normalised);
    }

    private void HandleMashTime(float secondsRemaining)
    {
        if (timerRing == null) return;
        float total = rescueEventController != null
            ? rescueEventController.ActiveMashWindowDuration : 3f;
        timerRing.fillAmount = total > 0f ? secondsRemaining / total : 0f;
        if (secondsRemaining < 1f) timerRing.color = dangerColour;
    }

    private void HandleCooldownTime(float secondsRemaining)
    {
        if (cooldownText) cooldownText.text = secondsRemaining.ToString("F1");
    }
}