using UnityEngine;
using System;

public class ZoneConditionEvaluator : MonoBehaviour, IConditionEvaluator
{
    [SerializeField] private GameObject[] triggerZoneObjects;

    private ITriggerZone[] triggerZones;
    private bool hasTriggered = false;

    public event Action OnConditionsMet;

    private void Awake()
    {
        triggerZones = new ITriggerZone[triggerZoneObjects.Length];

        for (int i = 0; i < triggerZoneObjects.Length; i++)
        {
            triggerZones[i] = triggerZoneObjects[i].GetComponent<ITriggerZone>();

            if (triggerZones[i] == null)
            {
                Debug.LogError($"{triggerZoneObjects[i].name} does not implement ITriggerZone!");
            }
        }
    }

    private void OnEnable()
    {
        foreach (var zone in triggerZones)
        {
            if (zone != null) zone.OnStateChanged += EvaluateConditions;
        }
    }

    private void OnDisable()
    {
        foreach (var zone in triggerZones)
        {
            if (zone != null) zone.OnStateChanged -= EvaluateConditions;
        }
    }

    private void EvaluateConditions()
    {
        if (hasTriggered) return;

        foreach (var zone in triggerZones)
        {
            if (!zone.IsActive) return;
        }

        hasTriggered = true;
        OnConditionsMet?.Invoke();
    }
}
