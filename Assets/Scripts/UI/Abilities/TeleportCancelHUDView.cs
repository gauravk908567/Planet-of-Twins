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
    }

    private void ShowPanel()
    {
        if (promptText != null) promptText.text = "Hold X to cancel";
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