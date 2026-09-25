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

    /// <summary>
    /// Fires only for COMBAT kills. Args = world position of the dead enemy + its visual size as a
    /// multiplier of a 2 m humanoid (1 ≈ human-sized; the death helix auto-fits from this).
    /// </summary>
    public event Action<Vector3, float> OnEnemyCombatKill;

    // Keep the parameterless event for any existing subscribers (SoulConvergenceSystem etc.)
    public event Action OnEnemyDied;

    /// <summary>
    /// Fires on any damage hit. Args = victim GameObject + full DamageData (attacker via Source).
    /// </summary>
    public event Action<GameObject, DamageData> OnEnemyDamaged;

    private readonly Dictionary<EnemyHealthComponent, Action> _deathHandlers
        = new Dictionary<EnemyHealthComponent, Action>();
    private readonly Dictionary<EnemyHealthComponent, Action<EnemyHealthComponent, DamageData>> _damageHandlers
        = new Dictionary<EnemyHealthComponent, Action<EnemyHealthComponent, DamageData>>();

    public void Register(EnemyHealthComponent enemy)
    {
        if (Instance == null) return;
        if (enemy == null || _deathHandlers.ContainsKey(enemy)) return;

        var captured = enemy;

        Action deathHandler = () =>
        {
            bool isPlayerKill = captured.LastDamageType == DamageType.Combat
                             || captured.LastDamageType == DamageType.Ability;
            if (!isPlayerKill) return;
            Vector3 pos = captured.LastDeathPosition;
            OnEnemyDied?.Invoke();
            OnEnemyCombatKill?.Invoke(pos, SizeScaleOf(captured));
        };

        Action<EnemyHealthComponent, DamageData> damageHandler = (hc, data) =>
            OnEnemyDamaged?.Invoke(hc.gameObject, data);

        _deathHandlers[enemy] = deathHandler;
        _damageHandlers[enemy] = damageHandler;
        enemy.OnDeath += deathHandler;
        enemy.OnDamaged += damageHandler;
    }

    // Visual size of the dying enemy as a multiplier of a 2 m humanoid — mesh renderers only
    // (particle renderers would inflate the bounds with aura FX). Rare event, so the sweep is fine.
    private static float SizeScaleOf(EnemyHealthComponent enemy)
    {
        var rends = enemy.GetComponentsInChildren<Renderer>();
        bool has = false;
        Bounds b = default;
        foreach (var r in rends)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) return 1f;
        return Mathf.Clamp(b.size.y / 2f, 0.5f, 3f);
    }

    public void Unregister(EnemyHealthComponent enemy)
    {
        if (enemy == null) return;
        if (_deathHandlers.TryGetValue(enemy, out var dh))
        {
            enemy.OnDeath -= dh;
            _deathHandlers.Remove(enemy);
        }
        if (_damageHandlers.TryGetValue(enemy, out var dmh))
        {
            enemy.OnDamaged -= dmh;
            _damageHandlers.Remove(enemy);
        }
    }
}
