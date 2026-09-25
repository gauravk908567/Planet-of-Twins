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
    private readonly CueBookData _cueBook;  // Slash on attacker swing (per-twin id); Hit on each enemy struck
    private readonly string _slashId;       // per-twin: on_meleeSlashKai / on_meleeSlashLyra — caller decides identity
    private readonly string _hitId;         // played on each enemy struck

    // hitAngle: 0.5f = 120° cone (wide), 0.7f = 90° cone (standard), 0.85f = 60° cone (narrow)
    public MeleeAttackStrategy(
        Transform attacker,
        float range,
        float damage,
        LayerMask targetMask,
        float hitAngle = 0.5f,
        int hitBufferSize = 10,
        IDamageMultiplier damageMultiplier = null,
        CueBookData cueBook = null,
        string slashId = FxIds.Player.Attack.on_meleeSlashKai,
        string hitId = FxIds.Player.Attack.on_meleeHit)
    {
        _attacker = attacker;
        _range = range;
        _damage = damage;
        _targetMask = targetMask;
        _hitAngle = hitAngle;
        _hitBuffer = new Collider[hitBufferSize];
        _damageMultiplier = damageMultiplier;
        _cueBook = cueBook;
        _slashId = slashId;
        _hitId = hitId;
    }

    public void ExecuteAttack()
    {
        if (_attacker == null) return;

        // Per-twin slash cue on the attacking twin — Follows the swing (Kai=electro, Lyra=stone; caller supplies id).
        if (_cueBook != null)
            FxManager.Instance?.PlayBook(_cueBook, _slashId, CueContext.Follow(_attacker));

        Vector3 origin = _attacker.position + _attacker.forward * (_range * 0.3f);
        float multiplier = _damageMultiplier?.DamageOutMultiplier ?? 1f;

        int hitCount = Physics.OverlapSphereNonAlloc(origin, _range, _hitBuffer, _targetMask);

        bool landed = false;   // punch hit-stop once per swing that CONNECTS (never on whiffs)
        for (int i = 0; i < hitCount; i++)
        {
            Vector3 dirToTarget = (_hitBuffer[i].transform.position - _attacker.position).normalized;
            if (Vector3.Dot(_attacker.forward, dirToTarget) < _hitAngle) continue;

            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            landed = true;
            damageable.TakeDamage(new DamageData(
                amount: _damage * multiplier,
                type: DamageType.Combat,
                source: _attacker.gameObject,
                hitPoint: _hitBuffer[i].transform.position));

            // Hit cue on enemy — World-anchored at chest height so it is visible.
            if (_cueBook != null)
            {
                Vector3 hitPos = _hitBuffer[i].transform.position + Vector3.up * 0.5f;
                FxManager.Instance?.PlayBook(_cueBook, _hitId, new CueContext(hitPos, _hitBuffer[i].transform.rotation));
            }

            // Shared hit spark at the strike point (Common book) — the SAME on_hiteffect the enemy-hits-twin path
            // uses, which is why it's World-anchored: both directions share one hit effect.
            CommonFx.Play(FxIds.Common.Effects.on_hiteffect,
                new CueContext(_hitBuffer[i].transform.position + Vector3.up * 0.5f));
        }

        // Contact feel: one hit-stop per landed swing (R10 — HitStopService is a TimeScaleService owner).
        if (landed) HitStopService.Instance?.Punch();
    }
}
