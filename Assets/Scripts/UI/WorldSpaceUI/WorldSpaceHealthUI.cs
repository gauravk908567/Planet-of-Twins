using System;
using UnityEngine;

public class WorldSpaceHealthUI : MonoBehaviour
{
    [Header("Source — assign ONE of these")]
    [SerializeField] private Player player;   // for Kai / Lyra / Soul
    [SerializeField] private EnemyHealthComponent enemyHealth; // for enemies

    [Header("View")]
    [SerializeField] private HealthBarView healthBarView;
    [SerializeField] private GameObject rootPanel;

    [Header("Visibility")]
    [SerializeField] private float hideDelay = 3f; // seconds after full HP before hiding

    private IHealthTracker _tracker;
    private float _hideTimer;
    private bool _isVisible;
    private bool _isFullHealth;

    private Action<float> _onHealthChanged;
    private Action _onDeath;

    private void Awake()
    {
        // Use player tracker if assigned, else use enemy health component
        _tracker = player != null
            ? player.HealthTracker
            : enemyHealth as IHealthTracker;

        _onHealthChanged = HandleHealthChanged;
        _onDeath = HandleDeath;
    }

    private void OnEnable()
    {
        if (_tracker == null) return;
        _tracker.OnDisplayHealthChanged += _onHealthChanged;
        _tracker.OnDeath += _onDeath;
        rootPanel?.SetActive(false); // hidden by default
    }

    private void OnDisable()
    {
        if (_tracker == null) return;
        _tracker.OnDisplayHealthChanged -= _onHealthChanged;
        _tracker.OnDeath -= _onDeath;
    }

    private void Update()
    {
        if (!_isVisible || !_isFullHealth) return;

        _hideTimer -= Time.deltaTime;
        if (_hideTimer <= 0f)
            SetVisible(false);
    }

    private void HandleHealthChanged(float displayHealth)
    {
        float normalised = _tracker.MaxHealth > 0f
            ? displayHealth / _tracker.MaxHealth : 0f;

        healthBarView?.SetFill(normalised);
        SetVisible(true);

        _isFullHealth = normalised >= 1f;
        if (_isFullHealth)
            _hideTimer = hideDelay; // start hide countdown
        else
            _hideTimer = 0f; // reset — not full yet
    }

    private void HandleDeath()
    {
        healthBarView?.SetFill(0f);
        healthBarView?.SetCriticalState(true);
        SetVisible(true);
        _isFullHealth = false;
    }

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        rootPanel?.SetActive(visible);
    }
}