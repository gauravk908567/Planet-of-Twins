using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central death bus. Filters to Combat kills only (zone despawns are Environmental).
/// Now passes the death world position so VFX spawners don't need a scene scan.
/// </summary>
public class EnemyDeathNotifier : MonoBehaviour, IEnemyDeathNotifier
{
    public static EnemyDeathNotifier Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Fires only for COMBAT kills. Arg = world position of the dead enemy.
    /// </summary>
    public event Action<Vector3> OnEnemyCombatKill;

    // Keep the parameterless event for any existing subscribers (SoulConvergenceSystem etc.)
    public event Action OnEnemyDied;

    private readonly Dictionary<EnemyHealthComponent, Action> _handlers
        = new Dictionary<EnemyHealthComponent, Action>();

    public void Register(EnemyHealthComponent enemy)
    {
        if (enemy == null || _handlers.ContainsKey(enemy)) return;

        var captured = enemy;
        Action handler = () =>
        {
            // Accept Combat (direct attack) AND Ability (Coalesce, future ability damage)
            // as player-sourced kills that count toward Soul Convergence.
            // Environmental (distance drain, zone despawn) is excluded.
            bool isPlayerKill = captured.LastDamageType == DamageType.Combat
                             || captured.LastDamageType == DamageType.Ability;
            if (!isPlayerKill) return;
            Vector3 pos = captured.LastDeathPosition;
            OnEnemyDied?.Invoke();
            OnEnemyCombatKill?.Invoke(pos);
        };

        _handlers[enemy] = handler;
        enemy.OnDeath += handler;
    }

    public void Unregister(EnemyHealthComponent enemy)
    {
        if (enemy == null) return;
        if (!_handlers.TryGetValue(enemy, out var handler)) return;
        enemy.OnDeath -= handler;
        _handlers.Remove(enemy);
    }
}