using UnityEngine;

/// <summary>
/// Chain Commander — Vethara
/// Commands: 2 TetherBreaker + 2 BasicMelee
///
/// Signature — ChainStrike (STUB):
///   When a TetherBreaker in the group has a twin pinned,
///   swings spiked chain at immobilised twin for high damage.
///
/// Squad, formation and death cascade: <see cref="CommanderEnemy"/>.
/// Brain: GOAPBrainChainCommander
/// </summary>
public class ChainCommander : CommanderEnemy
{
    [Header("Chain Strike")]
    [SerializeField] private float _strikeRange = 6f;
    [SerializeField] private float _strikeDamage = 40f;
    [SerializeField] private float _strikeCooldown = 12f;

    private float _lastStrike = -99f;

    // BUG-135 — a pooled commander comes back with its strike ready.
    public override void ResetForReuse()
    {
        base.ResetForReuse();
        _lastStrike = -99f;
    }

    // ── ChainStrike ────────────────────────────────────────────
    private void Update()
    {
        if (Health.IsDead) return;
        if (Time.time - _lastStrike < _strikeCooldown) return;
        if (!AnyTwinPinned()) return;
        TryChainStrike();
    }

    private bool AnyTwinPinned()
    {
        var soldiers = Soldiers;
        for (int i = 0; i < soldiers.Count; i++)   // indexed: a foreach over the interface allocates every frame
            if (soldiers[i] is TetherBreakerEnemy tb && tb.IsSprinting) return true;
        return false;
    }

    private void TryChainStrike()
    {
        foreach (var p in PlayerRoster.Twins)
        {
            if (!p.IsGrabbed) continue;
            if (Vector3.Distance(transform.position, p.transform.position) > _strikeRange) continue;
            _lastStrike = Time.time;
            PoTLog.AI?.Info($"STUB ChainStrike → {p.name} dmg={_strikeDamage}");
            // TODO: ChainStrike commander-ability VFX retired with EnemyVFXController; re-express via a
            // Manpu reaction cue or an Enraged mood transition when the commander archetypes are finished.
            // TODO: p.Health.TakeDamage(new DamageData(_strikeDamage, DamageType.Physical));
            return;
        }
    }
}
