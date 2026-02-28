using UnityEngine;

public class MeleeAttackStrategy : IAttackStrategy
{
    private Transform attacker;
    private float range;
    private int damage;
    private LayerMask targetMask;

    public MeleeAttackStrategy(
        Transform attacker,
        float range,
        int damage,
        LayerMask targetMask)
    {
        this.attacker = attacker;
        this.range = range;
        this.damage = damage;
        this.targetMask = targetMask;
    }

    public void ExecuteAttack()
    {
        Collider[] hits = Physics.OverlapSphere(
            attacker.position + attacker.forward * range * 0.5f,
            range,
            targetMask
        );

        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();

            if (damageable != null)
            {
                DamageData damageData = new DamageData(
                    damage,
                    attacker.gameObject,
                    hit.transform.position
                );

                damageable.TakeDamage(damageData);
            }
        }
    }
}