using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grand Summoner — Luminari
/// Commands: 1 Witness + 1 Ranged + 1 Siphon
///
/// Signature — Divine Shaft:
///   Every 3.5s buffs the lowest HP governed soldier.
///   Target gets +attack (×1.5) and +speed (×1.4) for 2.5s, tapering.
///   Projectile visual deferred to animation pass.
///
/// Squad, formation and death cascade: <see cref="CommanderEnemy"/>.
/// Brain: GOAPBrainGrandSummoner
/// </summary>
public class GrandSummoner : CommanderEnemy
{
    [Header("Divine Shaft")]
    [SerializeField] private float _interval = 3.5f;
    [SerializeField] private float _buffDuration = 2.5f;
    [SerializeField] private float _speedMult = 1.4f;
    [SerializeField] private float _damageMult = 1.5f;

    private float _lastShaft = 0f;

    // Soldiers under a Divine Shaft right now, with the speed each goes back to. Death ends them (BUG-136).
    private readonly List<Enemy> _buffTargets = new();
    private readonly List<float> _buffBaseSpeeds = new();

    // BUG-135 — a pooled commander comes back with its shaft timer at zero.
    public override void ResetForReuse()
    {
        base.ResetForReuse();
        _lastShaft = 0f;
        _buffTargets.Clear();
        _buffBaseSpeeds.Clear();
    }

    // BUG-136 — the pool return stops ApplyDivineShaft mid-taper, which left the soldier buffed for the rest of
    // its life. End every running buff while this commander still can.
    protected override void OnCommanderDeath()
    {
        for (int i = 0; i < _buffTargets.Count; i++)
        {
            var target = _buffTargets[i];
            if (target == null || target.Health.IsDead) continue;   // a dead soldier's own pool reset clears it
            target.AttackController.ClearDamageMultiplier();
            target.Movement.SetSpeed(_buffBaseSpeeds[i]);
        }
        _buffTargets.Clear();
        _buffBaseSpeeds.Clear();
    }

    // ── Divine Shaft ───────────────────────────────────────────
    private void Update()
    {
        if (Health.IsDead) return;
        if (Time.time - _lastShaft < _interval) return;
        if (Soldiers.Count == 0) return;
        FireDivineShaft();
    }

    private void FireDivineShaft()
    {
        Enemy target = null;
        float lowest = float.MaxValue;
        var soldiers = Soldiers;
        for (int i = 0; i < soldiers.Count; i++)
        {
            var s = soldiers[i];
            if (s == null || s.Health.IsDead) continue;
            float norm = s.Health.CurrentHealth / s.Health.MaxHealth;
            if (norm < lowest) { lowest = norm; target = s; }
        }
        if (target == null) return;

        _lastShaft = Time.time;
        StartCoroutine(ApplyDivineShaft(target));
        PoTLog.AI?.Info($"STUB DivineShaft → {target.name}");
        // TODO: DivineShaft commander-buff VFX retired with EnemyVFXController; re-express the buff via the
        // Common on_AlliesBuff cue (as Witness does) when the commander archetypes are finished.
    }

    private IEnumerator ApplyDivineShaft(Enemy target)
    {
        float baseSpeed = target.Data?.moveSpeed ?? 3.5f;
        target.AttackController.SetDamageMultiplier(_damageMult);
        target.Movement.SetSpeed(baseSpeed * _speedMult);
        _buffTargets.Add(target);
        _buffBaseSpeeds.Add(baseSpeed);
        // TODO: buffed-target VFX retired with EnemyVFXController — wire the Common on_AlliesBuff cue here
        // (Follow the target) when the commander archetypes are finished.

        float elapsed = 0f;   // scaled — the buff is gameplay (R10)
        while (elapsed < _buffDuration)
        {
            if (target == null || target.Health.IsDead) { EndBuffTracking(target); yield break; }
            elapsed += Time.deltaTime;
            float t = elapsed / _buffDuration;
            target.AttackController.SetDamageMultiplier(Mathf.Lerp(_damageMult, 1f, t));
            target.Movement.SetSpeed(Mathf.Lerp(baseSpeed * _speedMult, baseSpeed, t));
            yield return null;
        }
        target.AttackController.ClearDamageMultiplier();
        target.Movement.SetSpeed(baseSpeed);
        EndBuffTracking(target);
    }

    private void EndBuffTracking(Enemy target)
    {
        int index = _buffTargets.IndexOf(target);
        if (index < 0) return;
        _buffTargets.RemoveAt(index);
        _buffBaseSpeeds.RemoveAt(index);
    }
}
