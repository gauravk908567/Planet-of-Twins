using System;
using System.Collections.Generic;
using UnityEngine;

public class EndzoneTrigger : MonoBehaviour, ITriggerZone
{
    [SerializeField] private LayerMask targetLayer;

    public event Action OnStateChanged;
    public bool IsActive { get; private set; }

    private int playersInZone = 0;

    private HashSet<GameObject> objectsInZone = new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            if (objectsInZone.Add(other.gameObject))
            {
                UpdateState();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            if (objectsInZone.Remove(other.gameObject))
            {
                UpdateState();
            }
        }
    }

    private void UpdateState()
    {
        bool newState = objectsInZone.Count > 0;

        if (newState != IsActive)
        {
            IsActive = newState;
            OnStateChanged?.Invoke();
        }
    }
}
