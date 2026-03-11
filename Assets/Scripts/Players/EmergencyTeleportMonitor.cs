using System;
using UnityEngine;

public class EmergencyTeleportMonitor : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private MonoBehaviour inputProviderObject;
    [SerializeField] private MonoBehaviour twinSelectorObject;

    private bool _leftOverride;   // true = teleport forced available regardless of HP
    private bool _rightOverride;  // true = teleport forced available regardless of HP

    [Tooltip("GDD §8.3: Emergency gate available when DisplayHealth <= this value.")]
    [SerializeField] private float teleportHealthThreshold = 20f;

    private IInputProvider _input;
    private ITwinSelector _selector;
    private AbilityController _currentAbilityController;

    public event Action<bool> OnTwinDied;

    private bool _leftCritical;
    private bool _rightCritical;

    public bool IsEmergencyAvailable =>
        _leftCritical || _rightCritical ||   // HP-based
        _leftOverride || _rightOverride;

    public event Action<bool> OnEmergencyStateChanged;

    // FIX 2: Named delegate fields allocated once in Awake.
    // The same object instance is passed to both += and -=.
    private Action<float> _onLeftHealthChanged;
    private Action<float> _onRightHealthChanged;
    private Action _onLeftDeath;
    private Action _onRightDeath;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        _selector = twinSelectorObject as ITwinSelector;

        // FIX 1: No 'ref'. isLeft parameter routes to the correct field.
        // FIX 2: Allocated once — same instances used in += and -=.
        _onLeftHealthChanged = h => EvaluateThreshold(h, isLeft: true);
        _onRightHealthChanged = h => EvaluateThreshold(h, isLeft: false);
        _onLeftDeath = () => HandleDeath(isLeft: true);
        _onRightDeath = () => HandleDeath(isLeft: false);
    }

    private void OnEnable()
    {
        leftTwin.HealthTracker.OnDisplayHealthChanged += _onLeftHealthChanged;
        rightTwin.HealthTracker.OnDisplayHealthChanged += _onRightHealthChanged;
        leftTwin.HealthTracker.OnDeath += _onLeftDeath;
        rightTwin.HealthTracker.OnDeath += _onRightDeath;

        if (_selector != null)
            _selector.OnTwinSelected += OnTwinSelected;
    }

    private void OnDisable()
    {
        // -= now correctly removes the SAME delegate instances as +=
        leftTwin.HealthTracker.OnDisplayHealthChanged -= _onLeftHealthChanged;
        rightTwin.HealthTracker.OnDisplayHealthChanged -= _onRightHealthChanged;
        leftTwin.HealthTracker.OnDeath -= _onLeftDeath;
        rightTwin.HealthTracker.OnDeath -= _onRightDeath;

        if (_selector != null)
            _selector.OnTwinSelected -= OnTwinSelected;
    }

    private void OnTwinSelected(Transform t)
    {
        _currentAbilityController = t.GetComponent<AbilityController>();
    }

    private void Update()
    {
        if (!IsEmergencyAvailable || _currentAbilityController == null || _input == null)
            return;

        if (_input.GetTeleportHeld())
            _currentAbilityController.ShowTeleportPreview();

        if (_input.GetTeleportReleased())
        {
            _currentAbilityController.HideTeleportPreview();
            _currentAbilityController.ActivateTeleport();
        }
    }

    /// <summary>
    /// Called by RescueEventController when a trap grabs or releases a player.
    /// Bypasses the HP threshold — teleport is available whenever a twin is in trouble.
    /// </summary>
    public void SetEmergencyOverride(bool isLeft, bool active)
    {
        if (isLeft) _leftOverride = active;
        else _rightOverride = active;

        OnEmergencyStateChanged?.Invoke(IsEmergencyAvailable);
    }

    // FIX 1: ref removed. isLeft routes to the correct bool field.
    private void EvaluateThreshold(float displayHealth, bool isLeft)
    {
        bool wasCritical = isLeft ? _leftCritical : _rightCritical;
        bool isCritical = displayHealth <= teleportHealthThreshold;

        if (isLeft) _leftCritical = isCritical;
        else _rightCritical = isCritical;

        if (isCritical != wasCritical)
            OnEmergencyStateChanged?.Invoke(IsEmergencyAvailable);
    }

    private void HandleDeath(bool isLeft)
    {
        if (isLeft) _leftCritical = true;
        else _rightCritical = true;

        OnEmergencyStateChanged?.Invoke(IsEmergencyAvailable);
        OnTwinDied?.Invoke(isLeft); // ADD this line
    }
}