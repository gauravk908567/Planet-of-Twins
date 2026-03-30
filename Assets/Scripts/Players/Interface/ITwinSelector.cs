using System;
using UnityEngine;

public interface ITwinSelector
{
    Transform SelectedTransform { get; }
    event Action<Transform> OnTwinSelected;

    /// <summary>
    /// Force-selects a specific twin regardless of lock state.
    /// Used by EmpowerSystem to switch to the empowered twin on activation.
    /// TwinSelector.ForceSelect already implements this — surfaced here
    /// so EmpowerSystem doesn't need to cast to the concrete type.
    /// </summary>
    void ForceSelect(Player twin);
}