using System;
using UnityEngine;

/// <summary>
/// Authoritative health for enemies.
/// Implements IDamageable so MeleeAttackStrategy and EnemyAttackController
/// can both call TakeDamage via the interface without knowing the concrete type.
///
/// FIX: added OnHealthChanged event so EnemyHealthBarPresenter can drive the
/// world-space health bar slider without polling every frame.
/// FIX: added IDamageable — was missing, causing player/soul attacks to silently
/// skip damage (GetComponent<IDamageable>() returned null on the old component).
/// </summary>
public class EnemyHealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    private float _currentHealth;
    private DamageNumberDisplay _damageDisplay;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0f;

    /// <summary>DamageType of the killing hit — read by EnemyDeathNotifier.</summary>
    public DamageType LastDamageType { get; private set; } = DamageType.Environmental;

    /// <summary>World position at death — read by KillParticleSpawner.</summary>
    public Vector3 LastDeathPosition { get; private set; }

    /// <summary>Fires whenever health changes (damage or heal). Arg = normalised 0-1.</summary>
    public event Action<float> OnHealthChanged;

    /// <summary>
    /// Fires on every damage hit before death is evaluated.
    /// Args: this component, raw damage amount, world position of the hit.
    /// Used by SeveredEnemy (linked damage) and PenitentEnemy (trap reaction).
    /// </summary>
    public event Action<EnemyHealthComponent, float, Vector3> OnDamageTaken;

    public event Action OnDeath;

    private void Awake()
    {
        _currentHealth = maxHealth;
        _damageDisplay = GetComponentInChildren<DamageNumberDisplay>();
    }

    public void SetMaxHealth(float value)
    {
        maxHealth = value;
        _currentHealth = maxHealth;
        OnHealthChanged?.Invoke(1f);
    }

    public void TakeDamage(DamageData data)
    {
        if (IsDead) return;

        LastDamageType = data.Type;
        _currentHealth -= data.Amount;
        _damageDisplay?.ShowDamage(data.Amount);

        // Notify listeners (SeveredEnemy linked damage, PenitentEnemy, etc.)
        OnDamageTaken?.Invoke(this, data.Amount, transform.position);

        if (_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            LastDeathPosition = transform.position;
            OnHealthChanged?.Invoke(0f);
            OnDeath?.Invoke();
            MoodEventBus.AllyDied(gameObject);
            FactionEnergySystem.Instance?.OnEnemyDied();
        }
        else
        {
            OnHealthChanged?.Invoke(_currentHealth / maxHealth);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(_currentHealth / maxHealth);
    }

    public void ClearDeathSubscribers() => OnDeath = null;
}