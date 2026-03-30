using UnityEngine;
using TMPro;

public class WeaversGateHUDView : MonoBehaviour
{
    [SerializeField] private MonoBehaviour rescueActiveMono;
    [SerializeField] private AbilityController leftController;
    [SerializeField] private AbilityController rightController;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private string availableMessage = "Rescue Call (Weavers Gate Open)";

    private IRescueActive _rescueActive;

    private void Awake()
    {
        _rescueActive = rescueActiveMono as IRescueActive;
        // Set text once in Awake — never change it again, only alpha
        if (warningText != null)
            warningText.text = availableMessage;
        SetAlpha(0f);
    }

    private void Update()
    {
        if (warningText == null) return;
        bool rescueActive = _rescueActive?.HasActiveRescueTarget ?? false;
        bool gateReady = IsGateReady();
        SetAlpha(rescueActive && gateReady ? 1f : 0f);
    }

    private void SetAlpha(float alpha)
    {
        if (warningText == null) return;
        var c = warningText.color;
        c.a = alpha;
        warningText.color = c;
    }

    private bool IsGateReady()
    {
        var leftTA = leftController?.GetTeleportAbility();
        var rightTA = rightController?.GetTeleportAbility();
        if (leftTA != null && leftTA.CooldownProgress >= 1f) return true;
        if (rightTA != null && rightTA.CooldownProgress >= 1f) return true;
        return false;
    }
}