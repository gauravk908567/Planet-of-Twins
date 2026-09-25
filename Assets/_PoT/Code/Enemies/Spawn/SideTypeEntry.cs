using System;
using UnityEngine;

[Serializable]
public class SideTypeEntry
{
    [Tooltip("Enemy prefab.")]
    public GameObject prefab;

    [Tooltip("Stats SO — different SO = different variant.")]
    public EnemyData data;

    [Tooltip("Max of this type active on this side at once. 0 = unlimited.")]
    public int maxActiveOfType = 3;

    [Tooltip("Relative spawn weight. Higher = more frequent.")]
    [Range(1, 10)]
    public int weight = 1;

    [Header("Pairing")]
    [Tooltip("Partner preferences for this enemy type.\n" +
             "Leave null for solo-only spawning.")]
    public PairSpawnConfig pairConfig;

    [Header("Dark Energy")]
    [Tooltip("Starting dark energy for this enemy. 0 = use enemy default.")]
    [Range(0f, 1f)]
    public float darkEnergyBase = 0f;

    [Tooltip("Dark energy threshold to break death bond. 0 = use enemy default.")]
    [Range(0f, 1f)]
    public float bondBreakThreshold = 0f;

    [Tooltip("Dark energy threshold to unlock combos. 0 = use enemy default.")]
    [Range(0f, 1f)]
    public float comboThreshold = 0f;

    public bool HasDarkEnergyOverride =>
        darkEnergyBase > 0f || bondBreakThreshold > 0f || comboThreshold > 0f;
}