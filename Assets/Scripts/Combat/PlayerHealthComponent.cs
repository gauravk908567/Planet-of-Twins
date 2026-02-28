using UnityEngine;
using System;

public class PlayerHealthComponent : MonoBehaviour, IDamageable, IHealthUpdate
{
    [SerializeField] private float maxCombatHealth = 100f;
    [SerializeField] private float regenDelay = 3f;
    [SerializeField] private float regenRate = 5f; // health per second

    private float currentCombatHealth;
    private float distanceModifier = 1f;


    private float lastDamageTime;

    public event Action<PlayerHealthComponent, float> OnDamageTaken;
    public event Action<PlayerHealthComponent> OnDeath;
    public event Action<float> OnHealthUpdated;
    public float CombatHealth => currentCombatHealth;
    public float FinalHealth { get; private set; }
    public float MaxHealth => maxCombatHealth;
    private float previousFinalHealth;
    private void Awake()
    {
        currentCombatHealth = maxCombatHealth;
    }

    private void Update()
    {
        HandleRegen();
    }

    public void TakeDamage(DamageData damageData)
    {
        currentCombatHealth -= damageData.Amount;
        currentCombatHealth = Mathf.Clamp(currentCombatHealth, 0f, maxCombatHealth);

        lastDamageTime = Time.time;   // reset regen timer

        OnDamageTaken?.Invoke(this, damageData.Amount);

        CheckDeath();
        CalculateAndBroadcastHealth();
    }

    private void HandleRegen()
    {
        if (currentCombatHealth <= 0f)
            return;

        if (Time.time < lastDamageTime + regenDelay)
            return;

        if (currentCombatHealth >= maxCombatHealth)
            return;

        currentCombatHealth += regenRate * Time.deltaTime;
        currentCombatHealth = Mathf.Clamp(currentCombatHealth, 0f, maxCombatHealth);

        Debug.Log("health" + currentCombatHealth);
        CalculateAndBroadcastHealth();
    }

    public void SetDistanceModifier(float modifier)
    {
        distanceModifier = modifier;
        FinalHealth = currentCombatHealth * distanceModifier;
        CheckDeath();
        CalculateAndBroadcastHealth();
        Debug.Log("health" + currentCombatHealth);
    }

    private void CheckDeath()
    {
        if (FinalHealth <= 0f)
        {
            OnDeath?.Invoke(this);
            Debug.Log($"{gameObject.name} Died");
        }
    }
    private void CalculateAndBroadcastHealth()
    {
        FinalHealth = currentCombatHealth * distanceModifier;

        if (Mathf.Abs(FinalHealth - previousFinalHealth) > 0.01f)
        {
            OnHealthUpdated?.Invoke(FinalHealth);
            previousFinalHealth = FinalHealth;
        }
    }
}