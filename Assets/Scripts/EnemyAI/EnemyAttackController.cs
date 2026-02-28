using System.Collections;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask enemyLayer;

    private float attackCooldown = 1.5f;
    private float lastAttackTime;

    private float attackWindup = 0.3f;
    private bool isAttacking;

    public void TryAttack(bool isPossessed = false)
    {
        if (isAttacking) return;
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        StartCoroutine(AttackRoutine(isPossessed));
    }

    private IEnumerator AttackRoutine(bool isPossesed)
    {
        isAttacking = true;

        yield return new WaitForSeconds(attackWindup);

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            attackRange,
            isPossesed ? enemyLayer : playerLayer
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}