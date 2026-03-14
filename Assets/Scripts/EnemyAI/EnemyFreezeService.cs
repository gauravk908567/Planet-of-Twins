using UnityEngine;

/// <summary>
/// Freezes all active Enemy components by calling OnEffectStarted/OnEffectEnded
/// (the same interface stun and possession use). No persistent registry needed —
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
            e.OnEffectStarted();
        }

        Debug.Log("[EnemyFreezeService] All enemies frozen for QTE.");
    }

    public void UnfreezeAll()
    {
        if (!IsFrozen) return;
        IsFrozen = false;

        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (e == null) continue;
            e.OnEffectEnded();
        }

        Debug.Log("[EnemyFreezeService] All enemies unfrozen.");
    }
}