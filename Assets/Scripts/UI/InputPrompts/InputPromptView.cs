using UnityEngine;
using TMPro;

/// <summary>
/// F5 — Button HUD input prompt. A generic, reusable screen-space HUD element that shows the
/// ACTUAL bound key/button for one input action (e.g. "E" for Interact, "C" for Teleport),
/// read live from the Input System action asset via <see cref="IInputProvider.GetBindingDisplay"/>
/// — never hard-coded, so prompts stay correct under rebinding (F6).
///
/// This is the generic mechanism: one prefab per prompt, any action id. It serves every
/// contextual consumer (interact, rescue mash, teleport hold, ability, switch, …) identically
/// — the caller supplies the action name + label, or a controller toggles it via Show()/Hide().
///
/// Placement: screen-space HUD ONLY, in Persistent.unity (R9). Resolves the input provider in
/// Start() (R4) via TwinInputReader.Instance; fails loud + disables self if unresolved.
///
/// SETUP:
///   - Put this on a HUD element under a screen-space HUD canvas in Persistent.
///   - Assign _keyText (the "[key]" label) and optionally _actionLabel (the description).
///   - Set _actionName to the asset action (Interact/Teleport/Attack/Ability/Switch/…).
///   - Set _label to the description ("Pick up", "Teleport", …) — optional.
///   - _visibleOnStart = false for a contextual prompt driven by Show()/Hide().
/// </summary>
public class InputPromptView : MonoBehaviour
{
    [Header("Action → binding")]
    [Tooltip("Action name in PlanetOfTwins.inputactions (e.g. Interact, Teleport, Attack, Ability, Switch).")]
    [SerializeField] private string _actionName = "Interact";
    [Tooltip("Show the gamepad binding instead of keyboard/mouse.")]
    [SerializeField] private bool _preferGamepad = false;

    [Header("UI")]
    [Tooltip("Root object toggled by Show()/Hide(). If null, this GameObject is used.")]
    [SerializeField] private GameObject _root;
    [Tooltip("Displays the bound key/button glyph text (required).")]
    [SerializeField] private TMP_Text _keyText;
    [Tooltip("Optional description text next to the key (e.g. 'Pick up').")]
    [SerializeField] private TMP_Text _actionLabel;
    [SerializeField] private string _label = "";

    [Header("Behaviour")]
    [SerializeField] private bool _visibleOnStart = true;

    // Interface-typed (R4/SOLID) — concrete singleton only on the resolve line.
    private IInputProvider _input;

    private void Start()
    {
        _input = TwinInputReader.Instance;
        if (_input == null)
        {
            Debug.LogError("[InputPromptView] No IInputProvider (TwinInputReader.Instance null) — " +
                           "cannot resolve bindings. Is Persistent loaded? Disabling.", this);
            enabled = false;
            return;
        }

        if (_keyText == null)
            Debug.LogWarning("[InputPromptView] _keyText not assigned — key glyph will not show.", this);

        Refresh();
        SetVisible(_visibleOnStart);
    }

    /// <summary>Re-reads the live binding text. Call after a rebinding (F6) to update the glyph.</summary>
    public void Refresh()
    {
        if (_input == null) return;
        if (_keyText != null) _keyText.text = _input.GetBindingDisplay(_actionName, _preferGamepad);
        if (_actionLabel != null) _actionLabel.text = _label;
    }

    /// <summary>Change which action this prompt reflects at runtime, then refresh the glyph.</summary>
    public void SetAction(string actionName, string label = null)
    {
        _actionName = actionName;
        if (label != null) _label = label;
        Refresh();
    }

    public void Show() => SetVisible(true);
    public void Hide() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        // Prefer a dedicated child _root so this script's GameObject stays active (Start/Refresh
        // remain reachable). Falls back to toggling self when no _root is wired.
        GameObject target = _root != null ? _root : gameObject;
        target.SetActive(visible);
    }
}
