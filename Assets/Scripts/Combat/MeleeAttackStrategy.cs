using UnityEngine;

public class MeleeAttackStrategy : IAttackStrategy
{
    private readonly Transform _attacker;
    private readonly float _range;
    private readonly float _damage;
    private readonly LayerMask _targetMask;
    private readonly float _hitAngle;
    private readonly Collider[] _hitBuffer;
    private readonly IDamageMultiplier _damageMultiplier; // null = 1x, safe default

    // hitAngle: 0.5f = 120° cone (wide), 0.7f = 90° cone (standard), 0.85f = 60° cone (narrow)
    // damageMultiplier: inject SoulConvergenceSystem — null safe, defaults to 1x
    public MeleeAttackStrategy(
        Transform attacker,
        float range,
        float damage,
        LayerMask targetMask,
        float hitAngle = 0.5f,
        int hitBufferSize = 10,
        IDamageMultiplier damageMultiplier = null)
    {
        _attacker = attacker;
        _range = range;
        _damage = damage;
        _targetMask = targetMask;
        _hitAngle = hitAngle;
        _hitBuffer = new Collider[hitBufferSize];
        _damageMultiplier = damageMultiplier;
    }

    public void ExecuteAttack()
    {
        if (_attacker == null) return;

        Vector3 origin = _attacker.position + _attacker.forward * (_range * 0.3f);
        float multiplier = _damageMultiplier?.DamageOutMultiplier ?? 1f;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin, _range, _hitBuffer, _targetMask);

        for (int i = 0; i < hitCount; i++)
        {
            Vector3 dirToTarget = (_hitBuffer[i].transform.position - _attacker.position)
                                  .normalized;
            float dot = Vector3.Dot(_attacker.forward, dirToTarget);

            if (dot < _hitAngle) continue;

            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(new DamageData(
                amount: _damage * multiplier,
                type: DamageType.Combat,
                source: _attacker.gameObject,
                hitPoint: _hitBuffer[i].transform.position));
        }
    }
}