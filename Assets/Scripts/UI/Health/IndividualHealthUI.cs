using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IndividualHealthUI : MonoBehaviour
{
    [Tooltip("Drag Lyra or Kai's Player component here.")]
    [SerializeField] private Player playerObject;

    [Tooltip("Drag the HealthBarView on this same Canvas here.")]
    [SerializeField] private HealthBarView healthBarView;

    private IHealthTracker _tracker;

    // Stored delegates — same fix as EmergencyTeleportMonitor.
    // Lambda unsubscription silently fails; named fields don't.
    private Action<float> _onHealthChanged;
    private Action _onDeath;

    private void Awake()
    {
        // FIX: use the property Player exposes — no GetComponent needed
        _tracker = playerObject?.HealthTracker;

        if (_tracker == null)
        {
            Debug.LogError(
                $"[IndividualHealthUI] {gameObject.name}: playerObject is null or " +
                $"Player does not expose IHealthTracker. Check that HealthTracker " +
                $"property exists on Player.cs and PlayerHealthComponent is assigned.", this);
            return;
        }

        // Allocate delegates once so OnDisable can unsubscribe the same instances
        _onHealthChanged = HandleHealthChanged;
        _onDeath = HandleDeath;
    }

    private void OnEnable()
    {
        if (_tracker == null) return;
        _tracker.OnDisplayHealthChanged += _onHealthChanged;
        _tracker.OnDeath += _onDeath;
        Refresh(); // sync immediately in case health changed while disabled
    }

    private void OnDisable()
    {
        if (_tracker == null) return;
        _tracker.OnDisplayHealthChanged -= _onHealthChanged;
        _tracker.OnDeath -= _onDeath;
    }

    private void HandleHealthChanged(float displayHealth)
    {
        if (healthBarView == null) return;
        float normalised = _tracker.MaxHealth > 0f
            ? displayHealth / _tracker.MaxHealth
            : 0f;
        healthBarView.SetFill(normalised);
    }

    private void HandleDeath()
    {
        if (healthBarView == null) return;
        healthBarView.SetFill(0f);
        healthBarView.SetCriticalState(true);
    }

    private void Refresh()
    {
        if (healthBarView == null || _tracker == null) return;
        float normalised = _tracker.MaxHealth > 0f
            ? _tracker.DisplayHealth / _tracker.MaxHealth
            : 0f;
        healthBarView.SetFill(normalised);
    }
}

