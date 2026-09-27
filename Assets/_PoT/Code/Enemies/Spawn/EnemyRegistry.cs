using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Persistent singleton: every ACTIVE enemy, kept current by the enemies themselves (R5, the same pattern as
/// <see cref="SpawnZoneRegistry"/>). <c>Enemy.OnEnable</c> registers and <c>OnDisable</c> unregisters, so an enemy that
/// is pooled, killed or unloaded leaves at the same moment it stops being active. It holds the set
/// <c>FindObjectsByType&lt;Enemy&gt;()</c> returns, without the whole-scene search (P8.7, game.md §28).
///
/// READ IT ONE WAY (user, 2026-09-27: one consistent pattern): take a snapshot, then loop over the snapshot.
/// <code>using (EnemyRegistry.Snapshot(out List&lt;Enemy&gt; enemies)) { foreach (var e in enemies) … }</code>
/// The live list is never handed out, so a loop that damages, kills, spawns or disables enemies can't break itself, and
/// a nested call (Penitent A reflects onto B, which reflects back onto A) borrows its own list. The list comes from
/// Unity's <see cref="ListPool{T}"/>, so a snapshot makes no garbage.
///
/// WIRING: its own GameObject in Persistent.unity, next to SpawnZoneRegistry. OnEnemyRegistered / OnEnemyUnregistered
/// let a Persistent service react to arrivals (BUG-143: a QTE freeze catches enemies that spawn during it).
///
/// PROVEN EQUAL: a dev-only shadow check compared this set with <c>FindObjectsByType&lt;Enemy&gt;()</c> every 0.25 s
/// through P8.7 (0 mismatches in every session), then was deleted once every lookup had switched (2026-09-28).
/// </summary>
public class EnemyRegistry : MonoBehaviour
{
    public static EnemyRegistry Instance { get; private set; }

    private readonly List<Enemy> _enemies = new List<Enemy>();

    /// <summary>Fires when an enemy becomes active (spawned, issued from the pool, or its scene loaded).</summary>
    public event Action<Enemy> OnEnemyRegistered;
    /// <summary>Fires when an enemy stops being active (death → pool, despawn, scene unload).</summary>
    public event Action<Enemy> OnEnemyUnregistered;

    public int Count => _enemies.Count;

    private static bool _reportedMissing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        // Teardown order is undefined: drop every subscriber rather than keep delegates to dead objects (BUG-130).
        OnEnemyRegistered = null;
        OnEnemyUnregistered = null;
    }

    public void Register(Enemy enemy)
    {
        if (enemy == null || _enemies.Contains(enemy)) return;
        _enemies.Add(enemy);
        OnEnemyRegistered?.Invoke(enemy);
    }

    public void Unregister(Enemy enemy)
    {
        if (enemy == null || !_enemies.Remove(enemy)) return;
        OnEnemyUnregistered?.Invoke(enemy);
    }

    /// <summary>
    /// Every active enemy, copied into a pooled list the caller owns until the returned handle is disposed.
    /// Always dispose it: <c>using (EnemyRegistry.Snapshot(out var enemies)) { … }</c>, or
    /// <c>using var snapshot = EnemyRegistry.Snapshot(out var enemies);</c> when the loop runs to the end of the method.
    /// Never keep the list past that scope. No registry = an empty list and one loud error (Persistent isn't loaded),
    /// never a silent whole-scene search.
    /// </summary>
    public static PooledObject<List<Enemy>> Snapshot(out List<Enemy> enemies)
    {
        var handle = ListPool<Enemy>.Get(out enemies);
        var registry = Instance;
        if (registry != null)
        {
            registry.PurgeDestroyed();
            enemies.AddRange(registry._enemies);
        }
        else if (!_reportedMissing)
        {
            _reportedMissing = true;
            Debug.LogError("[EnemyRegistry] No EnemyRegistry: enemy lookups see NO enemies. " +
                           "It lives in Persistent.unity (is Persistent loaded?).");
        }
        return handle;
    }

    // R5: managers null-purge before reading (an entry destroyed without OnDisable, e.g. mid-teardown).
    private void PurgeDestroyed()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
            if (_enemies[i] == null) _enemies.RemoveAt(i);
    }
}
