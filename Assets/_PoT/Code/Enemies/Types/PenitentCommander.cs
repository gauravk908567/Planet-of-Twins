using System.Collections;
using UnityEngine;

/// <summary>
/// Penitent Commander — Vethara
/// Commands: 1 Witness + 1 Penitent + 2 GroupGrab
///
/// Signature — Dark Shield (STUB):
///   When any governed soldier takes 25%+ max HP in one hit,
///   all governed soldiers become invulnerable for 2.5s. 15s cooldown.
///
/// Soldier hits arrive through <see cref="CommanderEnemy.OnSoldierDamaged"/>.
/// Squad, formation and death cascade: <see cref="CommanderEnemy"/>.
/// Brain: GOAPBrainPenitentCommander
/// </summary>
public class PenitentCommander : CommanderEnemy
{
    [Header("Dark Shield")]
    [SerializeField] private float _damageThreshold = 0.25f;
    [SerializeField] private float _shieldDuration = 2.5f;
    [SerializeField] private float _shieldCooldown = 15f;

    private float _lastShield = -99f;
    private bool _shieldActive = false;

    // BUG-135 — a pooled commander comes back unshielded, its shield ready.
    public override void ResetForReuse()
    {
        base.ResetForReuse();
        _lastShield = -99f;
        _shieldActive = false;
    }

    // ── Dark Shield ────────────────────────────────────────────
    protected override void OnSoldierDamaged(Enemy soldier, float amount)
    {
        if (_shieldActive) return;
        if (Time.time - _lastShield < _shieldCooldown) return;
        if (amount / soldier.Health.MaxHealth >= _damageThreshold)
            StartCoroutine(ActivateDarkShield());
    }

    private IEnumerator ActivateDarkShield()
    {
        _shieldActive = true;
        _lastShield = Time.time;
        PoTLog.AI?.Info($"STUB DarkShield — {_shieldDuration}s");
        // TODO: DarkShield commander corruption VFX retired with EnemyVFXController; re-express via the
        // dark-energy corruption-state cue (EnemyDarkEnergy owns it — a held STATE aura, not a mood) when the
        // commander archetypes are finished.

        var soldiers = Soldiers;
        for (int i = 0; i < soldiers.Count; i++)
        {
            var s = soldiers[i];
            if (s == null || s.Health.IsDead) continue;
            // TODO: soldier shield corruption VFX retired — same corruption-state cue as above.
            PoTLog.AI?.Info($"STUB Shield → {s.name}");
            // TODO: s.Health.SetInvulnerable(true)
        }

        yield return new WaitForSeconds(_shieldDuration);   // scaled — gameplay (R10)
        EndDarkShield();
    }

    // BUG-136 — the pool return stops ActivateDarkShield mid-shield, so death ends it here, while the squad is
    // still registered. (Stub today; once SetInvulnerable exists, a shield cut short would otherwise stay on.)
    protected override void OnCommanderDeath()
    {
        if (_shieldActive) EndDarkShield();
    }

    private void EndDarkShield()
    {
        var soldiers = Soldiers;
        for (int i = 0; i < soldiers.Count; i++)
        {
            var s = soldiers[i];
            if (s == null || s.Health.IsDead) continue;
            // TODO: s.Health.SetInvulnerable(false)
        }
        _shieldActive = false;
    }
}
