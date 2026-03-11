using System;
using UnityEngine;

public interface ITwinSelector
{
    Transform SelectedTransform { get; }  // ADD if missing
    event System.Action<Transform> OnTwinSelected;
}