using UnityEngine;
using System.Collections.Generic;

public class TimeFactorManager : MonoBehaviour, ITimeFactorRegistry, ITimeFactorController, ICoroutineRunner
{
    private List<ITimeAffected> affectedEntities = new List<ITimeAffected>();
    private bool isEffectActive;

    /// <summary>True while a freeze effect holds the registered entities (soul cast / overview cam).
    /// A second system must NOT TriggerEffect/ResolveEffect while another owns it — check first.</summary>
    public bool IsEffectActive => isEffectActive;

    public static TimeFactorManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public void Register(ITimeAffected entity)
    {
        if (entity == null) return;
        if (!affectedEntities.Contains(entity)) affectedEntities.Add(entity);
        // Join an in-progress effect immediately: an enemy spawned (or pool-reused — its reset wipes
        // the frozen state it got while parked in the pool) DURING the soul cast ran at full speed
        // (BUG-069). Register is called on every spawn path, so this covers spawner/pool/debugger.
        if (isEffectActive) entity.OnEffectStarted();
    }
    public void Unregister(ITimeAffected entity)
    {
        affectedEntities.Remove(entity);
    }

    public void TriggerEffect()
    {
        if (isEffectActive) return;
        isEffectActive = true;
        PurgeDestroyed(); // 3.8: remove stale entries from unloaded scenes
        foreach (var item in affectedEntities) item.OnEffectStarted();
    }

    public void ResolveEffect()
    {
        if (!isEffectActive) return;
        isEffectActive = false;
        PurgeDestroyed(); // 3.8: remove stale entries from unloaded scenes
        foreach (var item in affectedEntities) item.OnEffectEnded();
    }

    // Destroyed MonoBehaviours fail Unity's overloaded == but pass C# null check.
    // Cast to Object to use Unity's destruction-aware equality.
    private void PurgeDestroyed()
        => affectedEntities.RemoveAll(e => e == null || (e is Object uo && uo == null));
}