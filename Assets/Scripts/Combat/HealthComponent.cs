using UnityEngine;
using System;
using System.Collections;

public class HealthComponent : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;

    [SerializeField] private int currentHealth;

    public int CurrentHealth => currentHealth;

    public event Action OnDeath;
    public event Action<HealthComponent, int> OnDamageTaken;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(DamageData damageData)
    {
        currentHealth -= damageData.Amount;

        ApplyKnockback(damageData);

        OnDamageTaken?.Invoke(this,damageData.Amount);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void ApplyKnockback(DamageData damageData)
    {
        CharacterController controller =
            GetComponent<CharacterController>();

        if (controller == null) return;

        Vector3 direction =
            (transform.position - damageData.Source.transform.position).normalized;

        controller.Move(direction * 0.5f);
    }

    private void Die()
    {
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        OnDeath?.Invoke();
        Destroy(gameObject);
    }
}