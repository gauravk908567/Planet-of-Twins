using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prewarm rows for <see cref="GameplayPool"/> (P16): each row instantiates
/// <c>count</c> inactive copies of <c>prefab</c> under its category root at Start, so first-use
/// hitches (chain + bomb prefabs are the heavy ones) happen at load, not in combat. Config only
/// (R7). Counts come from the TestLab/PSO trace pass — start small, grow from profiling.
/// </summary>
[CreateAssetMenu(fileName = "SpawnPrewarmProfile",
                 menuName = "PlanetOfTwins/Spawn/Prewarm Profile")]
public class SpawnPrewarmProfile : ScriptableObject
{
    [System.Serializable]
    public class Row
    {
        public GameObject prefab;
        [Min(0)] public int count = 2;
        public PoolCategory category = PoolCategory.Projectiles;
    }

    public List<Row> rows = new List<Row>();
}
