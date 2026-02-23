using UnityEngine;

public class AttackController : MonoBehaviour
{
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private LayerMask enemyLayer;

    private IAttackStrategy attackStrategy;

    private void Awake()
    {
        attackStrategy = new MeleeAttackStrategy(
            transform,
            attackRange,
            attackDamage,
            enemyLayer
        );
    }

    public void PerformAttack()
    {
        attackStrategy.ExecuteAttack();

        // TEMP TEST: Apply stun to nearby enemies
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            2f,
            enemyLayer
        );

        foreach (var hit in hits)
        {
            var status = hit.GetComponent<StatusEffectController>();
            if (status != null)
            {
                status.ApplyEffect(
                    new StunEffect(hit.gameObject, 2f)
                );
            }
        }
    }
}