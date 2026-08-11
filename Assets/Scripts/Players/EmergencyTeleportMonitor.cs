using System;
using UnityEngine;

/// <summary>
/// Condition gate only — tracks whether emergency teleport is available.
/// Does NOT handle input. TwinAbilityDispatcher reads IsEmergencyAvailable
/// and owns all teleport input dispatch.
///
/// Emergency is available when:
///   - Either twin's DisplayHealth is at or below teleportHealthThreshold, OR
///   - RescueEventController explicitly sets an override (twin grabbed by trap)
/// </summary>
public class EmergencyTeleportMonitor : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Tooltip("DisplayHealth threshold below which emergency gate becomes available.")]
    [SerializeField] private float teleportHealthThreshold = 20f;

    private bool _leftCritical;
    private bool _rightCritical;
    private bool _leftOverride;
    private bool _rightOverride;

    public bool IsEmergencyAvailable =>
        _leftCritical || _rightCritical || _leftOverride || _rightOverride;

    public event Action<bool> OnEmergencyStateChanged;
    public event Action<bool> OnTwinDied;

    private Action<float> _onLeftHealthChanged;
    private Action<float> _onRightHealthChanged;
    private Action _onLeftDeath;
    private Action _onRightDeath;

    private void Awake()
    {
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
    }

    private void OnDisable()
    {
        leftTwin.HealthTracker.OnDisplayHealthChanged -= _onLeftHealthChanged;
        rightTwin.HealthTracker.OnDisplayHealthChanged -= _onRightHealthChanged;
        leftTwin.HealthTracker.OnDeath -= _onLeftDeath;
        rightTwin.HealthTracker.OnDeath -= _onRightDeath;
    }

    /// <summary>
    /// Called by RescueEventController when a trap grabs or releases a player.
    /// Bypasses HP threshold — gate is available whenever a twin is in trouble.
    /// </summary>
    public void SetEmergencyOverride(bool isLeft, bool active)
    {
        if (isLeft) _leftOverride = active;
        else _rightOverride = active;
        OnEmergencyStateChanged?.Invoke(IsEmergencyAvailable);
    }

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
        OnTwinDied?.Invoke(isLeft);
    }
}