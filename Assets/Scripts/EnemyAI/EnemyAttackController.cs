using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private LayerMask playerLayer;

    private float attackCooldown = 1.5f;
    private float lastAttackTime;

    private float attackWindup = 0.3f;
    private bool isAttacking;

    public void TryAttack()
    {
        if (isAttacking) return;
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        yield return new WaitForSeconds(attackWindup);

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            attackRange,
            playerLayer
        );

        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();

            if (damageable != null)
            {
                DamageData damageData = new DamageData(
                    attackDamage,
                    gameObject,
                    hit.transform.position
                );

                damageable.TakeDamage(damageData);
            }
        }

        lastAttackTime = Time.time;
        isAttacking = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}