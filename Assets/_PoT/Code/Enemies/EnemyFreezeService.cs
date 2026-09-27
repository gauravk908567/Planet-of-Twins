using UnityEngine;

/// <summary>
/// Freezes all active enemies for a QTE. Each enemy is frozen with THIS service as its pause owner
/// (<see cref="Enemy.Freeze"/>), so the QTE's hold is separate from a stun, fear or the soul cast's freeze: ending the
/// QTE releases only the QTE's hold, and a stunned enemy finishes its stun (BUG-142). The enemies come from
/// <see cref="EnemyRegistry"/>'s snapshot (P8.7), not a scene search.
///
/// Place on any scene GameObject. QTEController holds a MonoBehaviour ref
/// that it casts to IEnemyFreezeService.
/// </summary>
public class EnemyFreezeService : MonoBehaviour, IEnemyFreezeService
{
    public bool IsFrozen { get; private set; } = false;

    public void FreezeAll()
    {
        if (IsFrozen) return;
        IsFrozen = true;

        using (EnemyRegistry.Snapshot(out var enemies))
            foreach (var e in enemies)
            {
                if (e == null) continue;
                e.Freeze(this);
            }

        PoTLog.QTE?.Info("All enemies frozen for QTE.");
    }

    public void UnfreezeAll()
    {
        if (!IsFrozen) return;
        IsFrozen = false;

        using (EnemyRegistry.Snapshot(out var enemies))
            foreach (var e in enemies)
            {
                if (e == null) continue;
                e.Unfreeze(this);
            }

        PoTLog.QTE?.Info("All enemies unfrozen.");
    }
}
