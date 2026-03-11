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

        foreach (var item in affectedEntities) item.OnEffectStarted();
    }

    public void ResolveEffect()
    {
        if (!isEffectActive) return;
        isEffectActive = false;

        foreach (var item in affectedEntities) item.OnEffectEnded();
    }
}