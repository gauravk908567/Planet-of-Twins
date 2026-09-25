using System;
using UnityEngine;

public interface IAbility
{
    void Initialize(GameObject owner);
    void TryActivate();
    void Tick();
}