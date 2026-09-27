using UnityEngine;

/// <summary>
/// Freezes all active enemies for a QTE. Each enemy is frozen with THIS service as its pause owner
/// (<see cref="Enemy.Freeze"/>), so the QTE's hold is separate from a stun, fear or the soul cast's freeze: ending the
/// QTE releases only the QTE's hold, and a stunned enemy finishes its stun (BUG-142). No persistent registry needed:
/// FreezeAll/UnfreezeAll are called infrequently (QTE start/end only).
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

        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
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

        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (e == null) continue;
            e.Unfreeze(this);
        }

        PoTLog.QTE?.Info("All enemies unfrozen.");
    }
}
