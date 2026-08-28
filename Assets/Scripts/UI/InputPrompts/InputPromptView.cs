using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Button HUD input prompt (F5 + item 5). A generic, reusable HUD element that shows the ACTUAL bound control for
/// one input action, read live from the Input System so prompts stay correct under rebinding (F6).
///
/// <para><b>Item 5 (glyphs):</b> when a glyph sprite exists for the resolved control it shows in <see cref="_keyIcon"/>
/// (the text label hides); otherwise it falls back to the binding TEXT in <see cref="_keyText"/>. The glyph is chosen
/// per the viewer's device — the occupant's paired device in couch, else the live last-used device — via
/// <see cref="InputGlyphResolver"/>, and re-resolves on a keyboard↔pad switch (<see cref="LastUsedDeviceTracker"/>).</para>
///
/// Placement: screen-space HUD in Persistent (R9). Resolves the input provider in Start (R4); fails loud + disables.
///
/// SETUP: assign <see cref="_keyIcon"/> (glyph target) and/or <see cref="_keyText"/> (text fallback); set
/// <see cref="_actionName"/> to the asset action (Interact/Teleport/Attack/…); <see cref="_visibleOnStart"/>=false
/// for a contextual prompt driven by Show()/Hide().
/// </summary>
public class InputPromptView : MonoBehaviour
{
    [Header("Action → binding")]
    [Tooltip("Action name in PlanetOfTwins.inputactions (e.g. Interact, Teleport, Attack, Ability, Empower).")]
    [SerializeField] private string _actionName = "Interact";
    [Tooltip("Text-fallback only: which family's TEXT to show when no glyph resolves. Sprite mode picks the device " +
             "automatically (occupant pairing / last-used).")]
    [SerializeField] private bool _preferGamepad = false;

    [Header("UI")]
    [Tooltip("Root object toggled by Show()/Hide(). If null, this GameObject is used.")]
    [SerializeField] private GameObject _root;
    [Tooltip("Glyph sprite target (item 5). When a glyph resolves it shows here and the text label hides.")]
    [SerializeField] private Image _keyIcon;
    [Tooltip("Bound key/button TEXT — the fallback shown when no glyph sprite exists for the control.")]
    [SerializeField] private TMP_Text _keyText;
    [Tooltip("Optional description text next to the key (e.g. 'Pick up').")]
    [SerializeField] private TMP_Text _actionLabel;
    [SerializeField] private string _label = "";

    [Header("Behaviour")]
    [Tooltip("Prefer the glyph SPRITE over text when one exists (needs _keyIcon wired).")]
    [SerializeField] private bool _preferSprite = true;
    [SerializeField] private bool _visibleOnStart = true;

    // Interface-typed (R4/SOLID) — concrete singleton only on the resolve line.
    private IInputProvider _input;

    private void Start()
    {
        _input = PlayerInputRouter.SharedInput;   // M0: shared-UI seam (falls back to TwinInputReader.Instance)
        if (_input == null)
        {
            Debug.LogError("[InputPromptView] No IInputProvider (PlayerInputRouter.SharedInput null) — " +
                           "cannot resolve bindings. Is Persistent loaded? Disabling.", this);
            enabled = false;
            return;
        }

        if (_keyIcon == null && _keyText == null)
            Debug.LogWarning("[InputPromptView] neither _keyIcon nor _keyText assigned — nothing to show.", this);

        Refresh();
        SetVisible(_visibleOnStart);
    }

    // Live device-switch (item 5): re-resolve when the player swaps keyboard↔pad (Overwatch-style). Named
    // handler, unsubscribed OnDisable (R8); the tracker's event spans scene loads.
    private void OnEnable()  => LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceSwitched;
    private void OnDisable() => LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceSwitched;
    private void OnDeviceSwitched(InputDeviceKind kind) { if (_input != null) Refresh(); }

    /// <summary>Re-resolves the glyph/text for the current device. Call after a rebinding (F6) too.</summary>
    public void Refresh()
    {
        if (_input == null) return;

        Sprite sprite = _preferSprite ? InputGlyphResolver.ResolveSprite(_input, _actionName, out _) : null;
        bool useSprite = sprite != null && _keyIcon != null;

        if (_keyIcon != null)
        {
            _keyIcon.gameObject.SetActive(useSprite);
            if (useSprite) _keyIcon.sprite = sprite;
        }
        if (_keyText != null)
        {
            _keyText.gameObject.SetActive(!useSprite);
            if (!useSprite) _keyText.text = _input.GetBindingDisplay(_actionName, _preferGamepad);
        }
        if (_actionLabel != null) _actionLabel.text = _label;
    }

    /// <summary>Change which action this prompt reflects at runtime, then refresh.</summary>
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
        // Prefer a dedicated child _root so this script's GameObject stays active (Start/Refresh reachable).
        GameObject target = _root != null ? _root : gameObject;
        target.SetActive(visible);
    }
}
