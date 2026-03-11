using System;
using UnityEngine;

[Serializable]
public class SideTypeEntry
{
    [Tooltip("Enemy prefab (BasicMeleeEnemy, RangedEnemy, etc)")]
    public GameObject prefab;

    [Tooltip("Stats SO — different SO = different variant, same prefab")]
    public EnemyData data;

    [Tooltip("Max of THIS type active on this side at once. 0 = unlimited.")]
    public int maxActiveOfType = 3;

    [Tooltip("Relative spawn weight for this side. Higher = more frequent.")]
    [Range(1, 10)]
    public int weight = 1;
}
