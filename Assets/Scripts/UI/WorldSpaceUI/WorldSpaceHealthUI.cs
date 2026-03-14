using System;
using UnityEngine;

/// <summary>
/// Drives a world-space health bar for both players and enemies.
///
/// FIX: EnemyHealthComponent never implemented IHealthTracker so the
/// cast was always null — bar never subscribed to damage events.
/// Enemy path now uses EnemyHealthComponent.OnHealthChanged directly
/// (already normalised 0-1) instead of going through IHealthTracker.
/// </summary>
public class WorldSpaceHealthUI : MonoBehaviour
{
    [Header("Source — assign ONE of these")]
    [SerializeField] private Player player;                    // for Kai / Lyra / Soul
    [SerializeField] private EnemyHealthComponent enemyHealth; // for enemies

    [Header("View")]
    [SerializeField] private HealthBarView healthBarView;
    [SerializeField] private GameObject rootPanel;

    [Header("Visibility")]
    [SerializeField] private float hideDelay = 3f;

    // ── Player path ───────────────────────────────────────────
    private IHealthTracker _tracker;

    // ── Enemy path ────────────────────────────────────────────
    private bool _usingEnemyPath = false;

    // ── Shared ────────────────────────────────────────────────
    private float _hideTimer;
    private bool _isVisible;
    private bool _isFullHealth;

    private Action<float> _onHealthChanged;
    private Action _onDeath;

    private void Awake()
    {
        if (player != null)
        {
            // Player path — use IHealthTracker (DisplayHealth is distance-adjusted)
            _tracker = player.HealthTracker;
            _usingEnemyPath = false;
        }
        else if (enemyHealth != null)
        {
            // Enemy path — EnemyHealthComponent.OnHealthChanged gives normalised 0-1
            _usingEnemyPath = true;
        }

        _onHealthChanged = HandleHealthChanged;
        _onDeath = HandleDeath;
    }

    private void OnEnable()
    {
        if (_usingEnemyPath)
        {
            if (enemyHealth == null) return;
            enemyHealth.OnHealthChanged += HandleEnemyHealthChanged;
            enemyHealth.OnDeath += _onDeath;
            // Reset bar to full on enable (handles pool reuse)
            healthBarView?.SetFill(1f);
            rootPanel?.SetActive(false);
        }
        else
        {
            if (_tracker == null) return;
            _tracker.OnDisplayHealthChanged += _onHealthChanged;
            _tracker.OnDeath += _onDeath;
            rootPanel?.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_usingEnemyPath)
        {
            if (enemyHealth == null) return;
            enemyHealth.OnHealthChanged -= HandleEnemyHealthChanged;
            enemyHealth.OnDeath -= _onDeath;
        }
        else
        {
            if (_tracker == null) return;
            _tracker.OnDisplayHealthChanged -= _onHealthChanged;
            _tracker.OnDeath -= _onDeath;
        }
    }

    private void Update()
    {
        if (!_isVisible || !_isFullHealth) return;
        _hideTimer -= Time.deltaTime;
        if (_hideTimer <= 0f)
            SetVisible(false);
    }

    // ── Enemy handler (normalised value comes in directly) ────
    private void HandleEnemyHealthChanged(float normalised)
    {
        healthBarView?.SetFill(normalised);
        SetVisible(true);

        _isFullHealth = normalised >= 1f;
        _hideTimer = _isFullHealth ? hideDelay : 0f;
    }

    // ── Player handler (raw DisplayHealth, needs dividing by max) ─
    private void HandleHealthChanged(float displayHealth)
    {
        float normalised = _tracker.MaxHealth > 0f
            ? displayHealth / _tracker.MaxHealth : 0f;

        healthBarView?.SetFill(normalised);
        SetVisible(true);

        _isFullHealth = normalised >= 1f;
        _hideTimer = _isFullHealth ? hideDelay : 0f;
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