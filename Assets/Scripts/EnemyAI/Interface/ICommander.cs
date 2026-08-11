using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interface implemented by all commander enemy types.
/// Allows GOAPGoalHoldFormation, EnemySpawner, and debug tools
/// to work with any commander without knowing the specific type.
/// </summary>
public interface ICommander
{
    bool IsAlive { get; }
    float CommandRadius { get; }
    IReadOnlyList<Enemy> Soldiers { get; }

    void RegisterSoldier(Enemy soldier);
    Vector3 GetSlotWorldPosition(Vector3 localOffset);
}