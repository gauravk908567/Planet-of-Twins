using UnityEngine;
using System.Collections.Generic;

public class TimeFactorManager : MonoBehaviour, ITimeFactorRegistry, ITimeFactorController, ICoroutineRunner
{
    private List<ITimeAffected> affectedEntities = new List<ITimeAffected>();
    private bool isEffectActive;

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
        if (!affectedEntities.Contains(entity)) affectedEntities.Add(entity);
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