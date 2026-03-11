using System;
using UnityEngine;

[Serializable]
public class SpawnEntry
{
    [Tooltip("The enemy prefab to spawn (BasicMeleeEnemy, RangedEnemy, etc)")]
    public GameObject prefab;

    [Tooltip("Stats SO — different SO = different variant, same prefab")]
    public EnemyData data;

    [Tooltip("Relative spawn weight. Higher = spawns more often. e.g. Melee=3, Ranged=1")]
    [Range(1, 10)]
    public int weight = 1;

    [Tooltip("Spawn side. Left enemies target Lyra, Right target Kai.")]
    public SpawnSide side = SpawnSide.Left;
}