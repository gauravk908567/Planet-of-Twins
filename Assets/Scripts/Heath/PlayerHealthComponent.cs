using UnityEngine;
using System;

public class PlayerHealthComponent : MonoBehaviour,
        IDamageable, IDistanceAffected, IHealthTracker
{
    [SerializeField] private float maxCombatHealth = 100f;

    private float _currentCombatHealth;
    private float _distanceModifier = 1f;
    private float _overMaxDistanceDrain = 0f;
    private bool _isDead;
    private bool _deathFired;

    private readonly HealthRegenHandler _regenHandler = new HealthRegenHandler();

    [SerializeField] private SharedHealthPool _sharedPool;

    public float CombatHealth => _currentCombatHealth;
    public float MaxHealth => maxCombatHealth;
    public float CurrentModifier => _distanceModifier;
    public bool IsDead => _isDead;

    private bool _invincible;
    private bool _isRegenActive = false;  // tracks regen state to fire events only on change
    public void SetInvincible(bool value) => _invincible = value;

    public float DisplayHealth
    {
        get
        {
            float compressed = _currentCombatHealth * _distanceModifier;
            float drained = _overMaxDistanceDrain * maxCombatHealth;
            return Mathf.Max(0f, compressed - drained);
        }
    }

    public event Action<float> OnDisplayHealthChanged;
    public event Action OnDeath;
    public event Action<PlayerHealthComponent, float, Vector3> OnDamageTaken; // component, amount, hitPoint
    public event Action OnRegenStarted;   // fires once when regen begins
    public event Action OnRegenStopped;   // fires once when regen stops or is interrupted

    private void Awake()
    {
        _currentCombatHealth = maxCombatHealth;
    }

    private void Update()
    {
        if (_isDead) return;

        float regenAmount = _regenHandler.GetRegenThisFrame(
            _currentCombatHealth, maxCombatHealth, Time.deltaTime);

        bool regenActive = regenAmount > 0f;

        // Fire events only when state changes — not every frame
        if (regenActive && !_isRegenActive)
        {
            _isRegenActive = true;
            OnRegenStarted?.Invoke();
        }
        else if (!regenActive && _isRegenActive)
        {
            _isRegenActive = false;
            OnRegenStopped?.Invoke();
        }

        if (regenAmount > 0f)
        {
            _currentCombatHealth = Mathf.Clamp(
                _currentCombatHealth + regenAmount, 0f, maxCombatHealth);
            BroadcastDisplayHealth();
        }
    }

    public void TakeDamage(DamageData damageData)
    {
        Debug.Log($"[Health] {gameObject.name} TakeDamage: _invincible={_invincible}, _isDead={_isDead}, HP={_currentCombatHealth}");
        if (_invincible) return;
        if (_isDead) return;

        float multipliedAmount = damageData.Amount *
            (_sharedPool != null ? _sharedPool.IncomingDamageMultiplier : 1f);

        _currentCombatHealth = Mathf.Clamp(
            _currentCombatHealth - multipliedAmount, 0f, maxCombatHealth);

        if (damageData.Type == DamageType.Combat)
            _regenHandler.OnCombatDamageTaken();

        OnDamageTaken?.Invoke(this, multipliedAmount, damageData.HitPoint);
        BroadcastDisplayHealth();

        if (_currentCombatHealth <= 0f && !_deathFired)
        {
            _deathFired = true;
            _isDead = true;
            OnDeath?.Invoke();
        }
    }

    public void SetDistanceModifier(float modifier)
    {
        if (_isDead) return;
        _distanceModifier = modifier;
        BroadcastDisplayHealth();
    }

    public void SetOverMaxDistanceCalculator(float drainFraction)
    {
        if (_isDead) return;
        _overMaxDistanceDrain = drainFraction;
        BroadcastDisplayHealth();
    }

    public void SetRegenUpgradeNode(int node) =>
        _regenHandler.SetUpgradeNode(node);

    public void SetDisplayHealthDirectly(float value)
    {
        _currentCombatHealth = Mathf.Clamp(value, 0, maxCombatHealth);
        BroadcastDisplayHealth();
    }

    public void ResetToFull()
    {
        _currentCombatHealth = maxCombatHealth;
        BroadcastDisplayHealth();
    }

    public void ResetToAlive()
    {
        _isDead = false;
        _deathFired = false;
    }

    public void Heal(float amount)
    {
        _currentCombatHealth = Mathf.Min(maxCombatHealth, _currentCombatHealth + amount);
        BroadcastDisplayHealth();
    }

    /// <summary>
    /// Full respawn restore — called by CheckpointManager on respawn.
    /// Restores HP to max, clears death flags, and resets distance drain.
    /// Safe to call in any state.
    /// </summary>
    public void RestoreToFull()
    {
        _isDead = false;
        _deathFired = false;
        _isRegenActive = false;
        _currentCombatHealth = maxCombatHealth;
        _overMaxDistanceDrain = 0f;
        _distanceModifier = 1f;
        _regenHandler.Reset();
        BroadcastDisplayHealth();
    }

    private float _previousDisplayHealth = -1f;

    private void BroadcastDisplayHealth()
    {
        float current = DisplayHealth;
        if (Mathf.Abs(current - _previousDisplayHealth) > 0.001f)
        {
            OnDisplayHealthChanged?.Invoke(current);
            _previousDisplayHealth = current;
        }
    }
}