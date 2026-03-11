using System;
using UnityEngine;

public interface IPossessEvents
{
    event Action OnPossessCast;
    event Action<GameObject> OnPossessApplied;
    event Action<GameObject> OnPossessEnded;
    void ReduceCurrentCooldown(float fraction);
}