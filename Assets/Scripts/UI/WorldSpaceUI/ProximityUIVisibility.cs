using UnityEngine;

/// <summary>
/// Shows or hides a world-space UI root panel based on player proximity.
///
/// SETUP:
///   Add this component to the world-space Canvas root GO (or any parent GO).
///   Wire _panelRoot to the UI panel you want to show/hide.
///   Wire _leftTwin and _rightTwin to both player transforms.
///   Set _showRadius — panel appears when EITHER twin is within this distance.
///
/// OPTIONAL:
///   _requireBothPlayers = true → panel only shows when BOTH twins are within radius.
///   (Used for Accord Rift interaction prompt.)
/// </summary>
public class ProximityUIVisibility : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The panel to show/hide based on proximity.")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Players")]
    [SerializeField] private Transform _leftTwin;
    [SerializeField] private Transform _rightTwin;

    [Header("Settings")]
    [Tooltip("Distance at which the panel becomes visible.")]
    [SerializeField] private float _showRadius = 5f;

    [Tooltip("If true, BOTH twins must be within radius to show. " +
             "If false, EITHER twin within radius is enough.")]
    [SerializeField] private bool _requireBothPlayers = false;

    [Tooltip("Small hysteresis buffer to prevent flickering at the boundary. " +
             "Panel hides at showRadius + hysteresis.")]
    [SerializeField] private float _hysteresis = 0.5f;

    private bool _isVisible;

    private void Awake()
    {
        _panelRoot?.SetActive(false);
        _isVisible = false;
    }

    private void Update()
    {
        bool shouldShow = CheckProximity();

        if (shouldShow != _isVisible)
        {
            _isVisible = shouldShow;
            _panelRoot?.SetActive(_isVisible);
        }
    }

    private bool CheckProximity()
    {
        // Use a slightly larger radius for hiding to prevent flicker
        float hideRadius = _showRadius + _hysteresis;

        bool leftIn = _leftTwin != null &&
                       Vector3.Distance(transform.position, _leftTwin.position)
                       <= (_isVisible ? hideRadius : _showRadius);

        bool rightIn = _rightTwin != null &&
                       Vector3.Distance(transform.position, _rightTwin.position)
                       <= (_isVisible ? hideRadius : _showRadius);

        return _requireBothPlayers ? (leftIn && rightIn) : (leftIn || rightIn);
    }

    // ── Public helpers ─────────────────────────────────────────────────
    /// Force hide regardless of proximity (e.g. during rescue or cutscene)
    public void ForceHide()
    {
        _isVisible = false;
        _panelRoot?.SetActive(false);
    }

    /// Force show regardless of proximity
    public void ForceShow()
    {
        _isVisible = true;
        _panelRoot?.SetActive(true);
    }
}