using UnityEngine;
using TMPro;

/// <summary>
/// F9 — World-space pickup UI. A small billboarded prompt shown over a pickupable world item
/// (e.g. the melee weapon pickup). Area-resident and fully self-contained: no cross-scene
/// serialized references (R2). The world-space canvas uses the WorldSpaceCanvasCamera pattern
/// (R9); billboarding is handled here so the prompt always faces the camera.
///
/// Generic: works for any pickup. Drop it on (or under) a pickup item, wire the child prompt
/// panel + label, and it shows when a twin is near and hides on pickup/exit. The bound-key
/// glyph is read live from the Input System via TwinInputReader.Instance (F5-consistent) when
/// _showKeyGlyph is on — useful once pickups become button-activated; for the current
/// auto-walk-over pickup the key line can be left off.
///
/// SETUP:
///   - This GO needs a trigger Collider (Is Trigger ON) sized to the show radius, OR set
///     _useOwnTrigger = false and drive Show()/Hide() from the pickup script.
///   - Child world-space Canvas with WorldSpaceCanvasCamera + this prompt panel; assign
///     _panelRoot (starts hidden). Optional _labelText / _keyText (TMP).
///   - Billboard: leave _billboard on; Y-only by default (upright).
/// </summary>
public class WorldSpacePickupPrompt : MonoBehaviour
{
    [Header("Prompt content")]
    [SerializeField] private GameObject _panelRoot;          // child panel — starts hidden
    [SerializeField] private TMP_Text _labelText;
    [SerializeField] private string _label = "Pick up";

    [Header("Key glyph (optional, F5-consistent)")]
    [Tooltip("Show the live bound key for an action (for button-activated pickups). " +
             "Leave off for auto-walk-over pickups.")]
    [SerializeField] private bool _showKeyGlyph = false;
    [SerializeField] private TMP_Text _keyText;
    [SerializeField] private string _actionName = "Interact";

    [Header("Visibility")]
    [Tooltip("Use this GO's own trigger collider to detect nearby twins (proximity-gated). " +
             "Turn OFF for an always-visible floating marker (correct for auto-walk-over " +
             "pickups, where a proximity gate would only flash as the item is consumed) — " +
             "the marker hides automatically when the pickup GameObject deactivates.")]
    [SerializeField] private bool _useOwnTrigger = false;
    [Tooltip("When NOT proximity-gated, show the marker as soon as the item is active.")]
    [SerializeField] private bool _showByDefault = true;

    [Header("Billboard")]
    [SerializeField] private bool _billboard = true;
    [SerializeField] private bool _lockX = false;
    [SerializeField] private bool _lockY = true;
    [SerializeField] private bool _lockZ = false;
    [Tooltip("Transform to billboard — defaults to _panelRoot's transform (or this).")]
    [SerializeField] private Transform _billboardTarget;

    private int _twinsInside;
    private Transform _cam;

    private void Awake()
    {
        _panelRoot?.SetActive(false);
        if (_billboardTarget == null)
            _billboardTarget = _panelRoot != null ? _panelRoot.transform : transform;
    }

    private void Start()
    {
        // Area-resident: Camera.main is the single Persistent MainCamera (R9). No serialized
        // cross-scene ref; resolved locally, fails soft (billboard just holds still if absent).
        if (Camera.main != null) _cam = Camera.main.transform;
        else Debug.LogWarning("[WorldSpacePickupPrompt] No Main Camera found — billboard disabled.", this);

        if (_showKeyGlyph && _keyText != null)
        {
            var input = PlayerInputRouter.SharedInput;   // M0: shared-UI seam (falls back to TwinInputReader.Instance)
            if (input != null) _keyText.text = input.GetBindingDisplay(_actionName);
            else Debug.LogWarning("[WorldSpacePickupPrompt] PlayerInputRouter.SharedInput null — key glyph blank.", this);
        }

        if (_labelText != null) _labelText.text = _label;

        // Not proximity-gated → act as an always-visible floating marker.
        if (!_useOwnTrigger && _showByDefault) SetVisible(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_useOwnTrigger || !IsTwin(other)) return;
        _twinsInside++;
        Evaluate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_useOwnTrigger || !IsTwin(other)) return;
        _twinsInside = Mathf.Max(0, _twinsInside - 1);
        Evaluate();
    }

    // A twin is Player-tagged; the rescue SoulPlayer must not trigger the pickup prompt.
    private static bool IsTwin(Collider other)
    {
        if (!other.CompareTag("Player")) return false;
        var player = other.GetComponentInParent<Player>();
        return player != null && !(player is SoulPlayer);
    }

    private void Evaluate() => SetVisible(_twinsInside > 0);

    /// <summary>External driver (pickup script) may show the prompt when _useOwnTrigger is off.</summary>
    public void Show() => SetVisible(true);
    /// <summary>Hide on pickup consumed / out of range.</summary>
    public void Hide() { _twinsInside = 0; SetVisible(false); }

    private void SetVisible(bool visible) => _panelRoot?.SetActive(visible);

    private void LateUpdate()
    {
        if (!_billboard || _cam == null || _billboardTarget == null) return;
        if (_panelRoot != null && !_panelRoot.activeInHierarchy) return; // no work while hidden

        Vector3 camEuler = _cam.rotation.eulerAngles;
        Vector3 cur = _billboardTarget.rotation.eulerAngles;
        _billboardTarget.rotation = Quaternion.Euler(
            _lockX ? camEuler.x : cur.x,
            _lockY ? camEuler.y : cur.y,
            _lockZ ? camEuler.z : cur.z);
    }
}
