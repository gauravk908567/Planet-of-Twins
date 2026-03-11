using System;
using UnityEngine;

public class EnemyHealthComponent : MonoBehaviour, IDamageable, IHealthTracker
{
    [SerializeField] private float maxHealth = 40f;

    private float _currentHealth;
    private bool _isDead;

    // ── IHealthTracker ─────────────────────────────────────────
    public float CombatHealth => _currentHealth;
    public float MaxHealth => maxHealth;
    public float DisplayHealth => _currentHealth;
    public float CurrentModifier => 1f;
    public bool IsDead => _isDead;

    public event Action<float> OnDisplayHealthChanged;
    public event Action OnDeath;



    // ── Legacy event — kept so existing subscribers don't break ─
    // WorldSpaceHealthUI uses OnDisplayHealthChanged (IHealthTracker).
    // Remove OnHealthChanged once all consumers are migrated.
    public event Action<float> OnHealthChanged;

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

    // ── IDamageable ────────────────────────────────────────────
    public void TakeDamage(DamageData damageData)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damageData.Amount);

        BroadcastHealth();

        if (_currentHealth <= 0f && !_isDead)
        {
            _isDead = true;
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        BroadcastHealth();
    }

    public void SetMaxHealth(float max)
    {
        maxHealth = max;
        _currentHealth = max;
        _isDead = false;
        BroadcastHealth();
    }

    private void BroadcastHealth()
    {
        float normalised = maxHealth > 0f ? _currentHealth / maxHealth : 0f;
        OnDisplayHealthChanged?.Invoke(_currentHealth);  // IHealthTracker — raw value
        OnHealthChanged?.Invoke(normalised);             // legacy — normalised
    }
}