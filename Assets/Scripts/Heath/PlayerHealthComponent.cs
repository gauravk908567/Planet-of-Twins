using UnityEngine;
using System;

public class PlayerHealthComponent : MonoBehaviour,
        IDamageable, IDistanceAffected, IHealthTracker
{
    [SerializeField] private float maxCombatHealth = 100f;

    // ── State ──────────────────────────────────────────────────
    private float _currentCombatHealth;
    private float _distanceModifier = 1f;
    private float _overMaxDistanceDrain = 0f;  // set by TwinBondManager
    private bool _isDead;
    private bool _deathFired;

    // ── Collaborators (SRP / DIP) ──────────────────────────────
    // HealthRegenHandler is a plain class — no MonoBehaviour overhead.
    private readonly HealthRegenHandler _regenHandler = new HealthRegenHandler();

    [Tooltip("Inject — drag SharedHealthPool here. Provides IncomingDamageMultiplier for Soul Convergence.")]
    [SerializeField] private SharedHealthPool _sharedPool;

    // ── IHealthTracker ─────────────────────────────────────────
    public float CombatHealth => _currentCombatHealth;
    public float MaxHealth => maxCombatHealth;
    public float CurrentModifier => _distanceModifier;
    public bool IsDead => _isDead;

    private bool _invincible;
    public void SetInvincible(bool value) => _invincible = value;

    /// <summary>
    /// GDD §6.3 ratio-preserving display value:
    ///   DisplayHealth = CombatHealth × distanceModifier − over18Drain
    /// The ratio of combat damage taken is preserved across distance zones.
    /// When returning to safety, HP expands back to CombatHealth automatically.
    /// The over18 drain reduces this further as an additive subtraction.
    /// </summary>
    public float DisplayHealth
    {
        get
        {
            float compressed = _currentCombatHealth * _distanceModifier;
            float drained = _overMaxDistanceDrain * maxCombatHealth;
            return Mathf.Max(0f, compressed - drained);
        }
    }

    // ── Events (IHealthTracker) ────────────────────────────────
    public event Action<float> OnDisplayHealthChanged;
    public event Action OnDeath;

    // Additional event for UI showing damage source
    public event Action<PlayerHealthComponent, float> OnDamageTaken;

    // ── Unity lifecycle ────────────────────────────────────────
    private void Awake()
    {
        _currentCombatHealth = maxCombatHealth;
    }

    private void Update()
    {
        if (_isDead) return;

        float regenAmount = _regenHandler.GetRegenThisFrame(
            _currentCombatHealth, maxCombatHealth, Time.deltaTime);

        if (regenAmount > 0f)
        {
            _currentCombatHealth = Mathf.Clamp(
                _currentCombatHealth + regenAmount, 0f, maxCombatHealth);
            BroadcastDisplayHealth();
        }
    }

    // ── IDamageable ────────────────────────────────────────────
    public void TakeDamage(DamageData damageData)
    {
        Debug.Log($"[Health] {gameObject.name} TakeDamage: _invincible={_invincible}, _isDead={_isDead}, HP={_currentCombatHealth}");
        if (_invincible) return;

        if (_isDead) return;

        float multipliedAmount = damageData.Amount * (_sharedPool != null ? _sharedPool.IncomingDamageMultiplier : 1f);

        _currentCombatHealth = Mathf.Clamp(
            _currentCombatHealth - multipliedAmount,
            0f, maxCombatHealth);

        if (damageData.Type == DamageType.Combat)
            _regenHandler.OnCombatDamageTaken();

        OnDamageTaken?.Invoke(this, multipliedAmount);
        BroadcastDisplayHealth();

        if (_currentCombatHealth <= 0f && !_deathFired)
        {
            _deathFired = true;
            _isDead = true;
            OnDeath?.Invoke();
        }
    }

    // ── IDistanceAffected ──────────────────────────────────────
    /// <summary>
    /// Called by TwinBondManager each frame with the latest 0–1 modifier.
    /// Does not trigger a regen reset — distance change is not combat damage.
    /// </summary>
    public void SetDistanceModifier(float modifier)
    {
        if (_isDead) return;
        _distanceModifier = modifier;
        BroadcastDisplayHealth();

    }

    // ── Over-18u drain (called by TwinBondManager) ─────────────
    /// <summary>
    /// Sets the accumulated drain fraction from the >18u system (0–1).
    /// A fraction of 1.0 means full health has been drained by distance.
    /// </summary>
    public void SetOverMaxDistanceCalculator(float drainFraction)
    {
        if (_isDead) return;
        _overMaxDistanceDrain = drainFraction;
        BroadcastDisplayHealth();

    }

    // ── Upgrade ────────────────────────────────────────────────
    public void SetRegenUpgradeNode(int node)
    {
        _regenHandler.SetUpgradeNode(node);
    }

    // ── Private helpers ────────────────────────────────────────
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

    /// <summary>
    /// Guarded by _isDead so OnDeath fires exactly once.
    /// </summary>

    public void SetDisplayHealthDirectly(float value)
    {
        _currentCombatHealth = Mathf.Clamp(value, 0, maxCombatHealth);
        BroadcastDisplayHealth();
    }

    // Called by SoulPlayer.ShouldSoulSleep(false) to reset soul HP each rescue
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
}