// Shows the remaining soul duration as a shrinking ring around the
// soul character (world-space) or as a HUD element.
//
// Attach to: HUD Canvas > SoulTimerPanel
// Wiring:
//   teleportAbilitySource → the AbilityController on Lyra or Kai
//                           (whichever twin is being set up as "left")
//                           OR wire both and subscribe to both.
//   timerRing             → Image (Filled, Radial 360)
//   rootPanel             → show/hide when soul is active
// ───────────────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.UI;

public class SoulTimerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Image timerRing;   // Filled, Radial 360

    [Header("Colours")]
    [SerializeField] private Color fullColour = new Color(0.4f, 0.7f, 1.0f, 1f);
    [SerializeField] private Color urgentColour = new Color(0.9f, 0.3f, 0.1f, 1f);

    private TeleportAbility _leftAbility;
    private TeleportAbility _rightAbility;

    // Called once by TwinAbilitySetup after abilities are built.
    // OR: expose as SerializeField and wire manually — either works.
    public void BindAbilities(TeleportAbility left, TeleportAbility right)
    {
        if (_leftAbility != null)
        {
            _leftAbility.OnSoulTimerUpdated -= HandleTimerUpdated;
            _leftAbility.OnSoulArrived -= ShowTimer;
        }
        if (_rightAbility != null)
        {
            _rightAbility.OnSoulTimerUpdated -= HandleTimerUpdated;
            _rightAbility.OnSoulArrived -= ShowTimer;
        }

        _leftAbility = left;
        _rightAbility = right;

        if (_leftAbility != null)
        {
            _leftAbility.OnSoulTimerUpdated += HandleTimerUpdated;
            _leftAbility.OnSoulArrived += ShowTimer;
        }
        if (_rightAbility != null)
        {
            _rightAbility.OnSoulTimerUpdated += HandleTimerUpdated;
            _rightAbility.OnSoulArrived += ShowTimer;
        }
    }

    private void ShowTimer()
    {
        rootPanel?.SetActive(true);
        if (timerRing)
        {
            timerRing.fillAmount = 1f;
            timerRing.color = fullColour;
        }
    }

    private void HandleTimerUpdated(float normalised)
    {
        if (timerRing == null) return;
        timerRing.fillAmount = normalised;
        timerRing.color = Color.Lerp(urgentColour, fullColour, normalised);

        if (normalised <= 0f)
            rootPanel?.SetActive(false);
    }
}

