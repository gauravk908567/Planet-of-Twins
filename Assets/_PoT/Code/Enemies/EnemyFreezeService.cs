using UnityEngine;

/// <summary>
/// Freezes all active enemies for a QTE. Each enemy is frozen with THIS service as its pause owner
/// (<see cref="Enemy.Freeze"/>), so the QTE's hold is separate from a stun, fear or the soul cast's freeze: ending the
/// QTE releases only the QTE's hold, and a stunned enemy finishes its stun (BUG-142). The enemies come from
/// <see cref="EnemyRegistry"/>'s snapshot (P8.7), not a scene search. An enemy that spawns WHILE the QTE holds the
/// world joins the freeze through the registry's arrival event (BUG-143), as <c>TimeFactorManager.Register</c> does for
/// the soul cast, and <see cref="UnfreezeAll"/> releases it with the rest.
///
/// Exactly ONE instance, on QTEManager's GameObject in Persistent: QTEManager holds a MonoBehaviour ref that it casts
/// to IEnemyFreezeService. (A second copy on GameSystem was removed 2026-09-28: two services meant two IsFrozen flags.)
/// </summary>
public class EnemyFreezeService : MonoBehaviour, IEnemyFreezeService
{
    public bool IsFrozen { get; private set; } = false;

    private EnemyRegistry _registry;

    // R8: Start resolves others (the registry is a Persistent singleton beside this service); named handler.
    private void Start()
    {
        _registry = EnemyRegistry.Instance;
        if (_registry == null)
        {
            Debug.LogError("[EnemyFreezeService] No EnemyRegistry (it lives in Persistent.unity): enemies that spawn " +
                           "during a QTE won't be frozen (BUG-143).", this);
            enabled = false;
            return;
        }
        _registry.OnEnemyRegistered += HandleEnemyRegistered;
    }

    private void OnDestroy()
    {
        if (_registry != null) _registry.OnEnemyRegistered -= HandleEnemyRegistered;
    }

    // BUG-143: a new arrival joins a freeze in progress. It registers in OnEnable, before the pool places it and turns
    // its NavMeshAgent on, so the movement stop is a no-op here; the brain hold is what counts, and the spawn reveal's
    // own pause stops the agent once it's placed. The reveal can't release this hold (owner-held pauses, BUG-142).
    private void HandleEnemyRegistered(Enemy enemy)
    {
        if (IsFrozen && enemy != null) enemy.Freeze(this);
    }

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
