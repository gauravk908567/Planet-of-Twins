using System;
using UnityEngine;

public interface ISelectionBroadcaster
{
    event Action<Transform> OnTwinSelected;
}