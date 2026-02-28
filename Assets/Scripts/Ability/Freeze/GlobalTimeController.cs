using UnityEngine;
using System.Collections.Generic;

public class GlobalTimeController : MonoBehaviour
{
    public static GlobalTimeController Instance;

    private List<ITimeFreezable> freezables = new List<ITimeFreezable>();

    private bool isFrozen;

    private void Awake()
    {
        Instance = this;
    }

    public void Register(ITimeFreezable freezable)
    {
        if (!freezables.Contains(freezable))
            freezables.Add(freezable);
    }

    public void Unregister(ITimeFreezable freezable)
    {
        freezables.Remove(freezable);
    }

    public void FreezeWorld()
    {
        if (isFrozen) return;

        isFrozen = true;

        foreach (var item in freezables)
            item.OnFreeze();
    }

    public void UnfreezeWorld()
    {
        if (!isFrozen) return;

        isFrozen = false;

        foreach (var item in freezables)
            item.OnUnfreeze();
    }
}