using System;
using UnityEngine;

/// <summary>
/// Pulses the BOND meter's external flash so the player reads when the bond is under threat and
/// when a rescue is available — the feedback that makes Weaver's Gate legible (game.md §4.1).
///
/// The bond flashes on:
///   • either twin taking damage        (<see cref="PlayerHealthComponent.OnDamageTaken"/>)
///   • a twin going down / rescue armed  (<c>RescueEventController.OnPlayerInDanger</c> / <c>OnActiveTargetChanged</c>)
///   • the emergency teleport arming     (<c>EmergencyTeleportMonitor.OnEmergencyStateChanged</c>)
///
/// A one-shot pulse decays back to zero. While a rescue is available or the emergency gate stays
/// armed, a gentle heartbeat keeps re-pulsing so the "cast Weaver's Gate" cue persists instead of
/// firing once and vanishing.
///
/// This ONLY drives the EXTERNAL flash channel (<see cref="UIBarView.SetFlash"/>), which UIBarView
/// max's with its own low-value warning flash — so the two never fight and the low-health pulse
/// stays owned by the bar. R1: bond bars, twins and the rescue/emergency monitors all live in
/// Persistent, so serialized refs are correct (no lookup). R8: named handlers, subscribed in Start,
/// unsubscribed in OnDestroy.
/// </summary>
[DisallowMultipleComponent]
public class BondFlashDriver : MonoBehaviour
{
    [Header("Bond meter to flash (the shared emblem)")]
    [Tooltip("The shared emblem's UIBarHealthView — SetFlash is pulsed on both of its halves at once.")]
    [SerializeField] private UIBarHealthView _bondView;

    [Header("Twins (damage flash)")]
    [SerializeField] private PlayerHealthComponent _leftHealth;
    [SerializeField] private PlayerHealthComponent _rightHealth;

    [Header("Rescue signals (optional — damage flash still works without them)")]
    [SerializeField] private RescueEventController _rescue;
    [SerializeField] private EmergencyTeleportMonitor _emergency;

    [Header("Pulse shape")]
    [Tooltip("Flash amount a fresh pulse starts at (0..1). 1 briefly washes the bar toward white.")]
    [SerializeField, Range(0f, 1f)] private float _pulseAmount = 1f;
    [Tooltip("How fast a one-shot pulse decays back to 0, in flash-units per second.")]
    [SerializeField] private float _decayPerSecond = 2.5f;

    [Header("Rescue-available heartbeat")]
    [Tooltip("While a rescue is available or the emergency gate is armed, re-pulse on this interval " +
             "(seconds) so the cue keeps drawing the eye. 0 disables the heartbeat.")]
    [SerializeField] private float _heartbeatInterval = 0.9f;
    [Tooltip("Amplitude of each heartbeat re-pulse (0..1). Gentler than a damage pulse.")]
    [SerializeField, Range(0f, 1f)] private float _heartbeatAmount = 0.7f;

    private float _flash;             // current external-flash level pushed to the bars
    private bool  _rescueAvailable;   // a rescue target is currently active
    private bool  _emergencyArmed;    // emergency teleport gate is currently available
    private float _heartbeatTimer;

    // Named handlers (R8) — same delegate instance for += and -=.
    private Action<PlayerHealthComponent, float, Vector3> _onLeftDamaged;
    private Action<PlayerHealthComponent, float, Vector3> _onRightDamaged;
    private Action<Player> _onPlayerInDanger;
    private Action<IRescueTarget> _onActiveTargetChanged;
    private Action<bool> _onEmergencyChanged;

    private void Awake()
    {
        _onLeftDamaged  = (_, __, ___) => Pulse(_pulseAmount);
        _onRightDamaged = (_, __, ___) => Pulse(_pulseAmount);
        _onPlayerInDanger = _ => Pulse(_pulseAmount);
        _onActiveTargetChanged = target =>
        {
            _rescueAvailable = target != null;
            if (_rescueAvailable) { Pulse(_pulseAmount); _heartbeatTimer = 0f; }
        };
        _onEmergencyChanged = armed =>
        {
            _emergencyArmed = armed;
            if (armed) { Pulse(_pulseAmount); _heartbeatTimer = 0f; }
        };
    }

    private void Start()
    {
        if (_bondView == null)
        {
            Debug.LogError($"[{nameof(BondFlashDriver)}] No bond view assigned — nothing to flash. " +
                           "Disabling.", this);
            enabled = false;
            return;
        }

        if (_leftHealth != null)  _leftHealth.OnDamageTaken  += _onLeftDamaged;
        if (_rightHealth != null) _rightHealth.OnDamageTaken += _onRightDamaged;
        if (_leftHealth == null && _rightHealth == null)
            Debug.LogWarning($"[{nameof(BondFlashDriver)}] No twin health components assigned — the " +
                             "bond will not flash on damage.", this);

        if (_rescue != null)
        {
            _rescue.OnPlayerInDanger += _onPlayerInDanger;
            _rescue.OnActiveTargetChanged += _onActiveTargetChanged;
        }
        if (_emergency != null)
            _emergency.OnEmergencyStateChanged += _onEmergencyChanged;
    }

    private void OnDestroy()
    {
        if (_leftHealth != null)  _leftHealth.OnDamageTaken  -= _onLeftDamaged;
        if (_rightHealth != null) _rightHealth.OnDamageTaken -= _onRightDamaged;
        if (_rescue != null)
        {
            _rescue.OnPlayerInDanger -= _onPlayerInDanger;
            _rescue.OnActiveTargetChanged -= _onActiveTargetChanged;
        }
        if (_emergency != null)
            _emergency.OnEmergencyStateChanged -= _onEmergencyChanged;
    }

    private void Pulse(float amount)
    {
        // Rising-edge only: a fresh pulse never cuts an already-brighter flash short.
        if (amount > _flash) _flash = Mathf.Clamp01(amount);
    }

    private void Update()
    {
        // R10: scaled time — the bond bar's own flash pulse is scaled too (UIBarView default), so
        // the two stay in step and both freeze under Setsuna/pause. A warning flash while the game
        // is frozen would be moot anyway.
        float dt = Time.deltaTime;

        // Heartbeat while a rescue is available or the emergency gate is armed.
        if ((_rescueAvailable || _emergencyArmed) && _heartbeatInterval > 0f)
        {
            _heartbeatTimer += dt;
            if (_heartbeatTimer >= _heartbeatInterval)
            {
                _heartbeatTimer = 0f;
                Pulse(_heartbeatAmount);
            }
        }
        else
        {
            _heartbeatTimer = 0f;
        }

        if (_flash > 0f)
            _flash = Mathf.MoveTowards(_flash, 0f, _decayPerSecond * dt);

        _bondView.SetFlash(_flash);
    }
}
