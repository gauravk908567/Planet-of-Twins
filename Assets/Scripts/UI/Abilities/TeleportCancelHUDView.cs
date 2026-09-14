using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows "Hold X to cancel" during the teleport cancel window.
///
/// SETUP:
///   - This GameObject (script owner) must ALWAYS stay active in the scene.
///   - Create a child GameObject "CancelPanel" with the visual content.
///   - Assign CancelPanel to cancelPanel slot — it starts inactive.
///   - Assign TMP_Text and fill ring Image inside CancelPanel.
///   - Assign both AbilityControllers from left/right twins.
/// </summary>
public class TeleportCancelHUDView : MonoBehaviour
{
    [Header("UI — child panel, starts inactive")]
    [SerializeField] private GameObject cancelPanel;  // child — toggled on/off
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image cancelFillRing;

    [Header("Prompt")]
    [Tooltip("Device-aware prompt template. The {Cancel} token becomes the live cancel glyph (keyboard X / pad " +
             "button) via InputGlyphText, so it stays correct under rebinding and on a keyboard↔pad switch.")]
    [SerializeField] private string _promptTemplate = "Hold {Cancel} to cancel";

    [Header("Twin controllers")]
    [SerializeField] private AbilityController leftController;
    [SerializeField] private AbilityController rightController;

    private TeleportAbility _leftTA;
    private TeleportAbility _rightTA;

    private void Awake()
    {
        cancelPanel?.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(ResolveAfterSetup());
    }

    private IEnumerator ResolveAfterSetup()
    {
        yield return null; // wait one frame for TwinAbilitySetup.Start()

        _leftTA = leftController?.GetTeleportAbility();
        _rightTA = rightController?.GetTeleportAbility();

        if (_leftTA != null)
        {
            _leftTA.OnCancelWindowOpened += ShowPanel;
            _leftTA.OnCancelWindowClosed += HidePanel;
            _leftTA.OnCancelProgressUpdated += UpdateRing;
        }
        else Debug.LogWarning("[TeleportCancelHUDView] Left TeleportAbility null.", this);

        if (_rightTA != null)
        {
            _rightTA.OnCancelWindowOpened += ShowPanel;
            _rightTA.OnCancelWindowClosed += HidePanel;
            _rightTA.OnCancelProgressUpdated += UpdateRing;
        }
        else Debug.LogWarning("[TeleportCancelHUDView] Right TeleportAbility null.", this);

        // Item-5 glyphs: re-resolve the {Cancel} glyph when the player swaps keyboard↔pad while the window is
        // open (Overwatch-style). Named handler, unsubscribed in OnDestroy (R8); the tracker spans scene loads.
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceSwitched;
    }

    private void OnDestroy()
    {
        if (_leftTA != null)
        {
            _leftTA.OnCancelWindowOpened -= ShowPanel;
            _leftTA.OnCancelWindowClosed -= HidePanel;
            _leftTA.OnCancelProgressUpdated -= UpdateRing;
        }
        if (_rightTA != null)
        {
            _rightTA.OnCancelWindowOpened -= ShowPanel;
            _rightTA.OnCancelWindowClosed -= HidePanel;
            _rightTA.OnCancelProgressUpdated -= UpdateRing;
        }
        LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceSwitched;
    }

    private void OnDeviceSwitched(InputDeviceKind kind)
    {
        // Only the visible prompt needs re-resolving; the panel owner stays active always.
        if (cancelPanel != null && cancelPanel.activeSelf) ApplyPrompt();
    }

    // Device-aware "Hold <cancel glyph> to cancel": {Cancel} → the live keyboard/pad glyph for whoever last
    // acted (shared cursor), read from the binding so it stays correct under rebinding (F6).
    private void ApplyPrompt()
    {
        if (promptText != null)
            InputGlyphText.Apply(promptText, _promptTemplate, PlayerInputRouter.SharedInput);
    }

    private void ShowPanel()
    {
        ApplyPrompt();
        UpdateRing(0f);
        cancelPanel?.SetActive(true);
    }

    private void HidePanel()
    {
        cancelPanel?.SetActive(false);
    }

    private void UpdateRing(float progress)
    {
        if (cancelFillRing != null)
            cancelFillRing.fillAmount = Mathf.Clamp01(progress);
    }
}