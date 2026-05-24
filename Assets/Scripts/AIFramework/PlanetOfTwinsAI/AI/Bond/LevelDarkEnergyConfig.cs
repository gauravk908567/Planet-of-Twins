using UnityEngine;

/// <summary>
/// ScriptableObject defining dark energy scaling per level.
/// Apply to all enemies at level load via EnemyDarkEnergy.ApplyLevelScaling().
///
/// CREATE: Right-click → Create → PlanetOfTwins/AI/Level Dark Energy Config
///
/// EXAMPLE progression:
///   Level 1: base=0.10, threshold=0.90 (hard to break bond)
///   Level 3: base=0.25, threshold=0.85
///   Level 5: base=0.40, threshold=0.80 (easier to break bond, more dangerous)
///   Level 8: base=0.55, threshold=0.75 (enemies frequently break bonds)
/// </summary>
[CreateAssetMenu(fileName = "LevelDarkEnergyConfig",
                 menuName = "PlanetOfTwins/AI/Level Dark Energy Config")]
public class LevelDarkEnergyConfig : ScriptableObject
{
    [System.Serializable]
    public class EnemyDarkEnergyScale
    {
        public AlliedClanType clanType;
        [Range(0f, 1f)] public float baseEnergy = 0.1f;
        [Range(0f, 1f)] public float bondThreshold = 0.9f;
        [Range(0f, 1f)] public float comboThreshold = 0.6f;
    }

    [Header("Level Identity")]
    public string levelName;
    public int levelIndex;

    [Header("Per-clan scaling")]
    [Tooltip("One entry per AlliedClanType. Enemies read their own clan entry.")]
    public EnemyDarkEnergyScale[] clanScales;

    [Header("Global combo ceiling")]
    [Tooltip("Max power strength multiplier for combos at this level. Increases as game progresses.")]
    [Range(0.5f, 2f)] public float comboPowerCeiling = 1.0f;

    public EnemyDarkEnergyScale GetScaleForClan(AlliedClanType clan)
    {
        if (clanScales == null) return null;
        foreach (var s in clanScales)
            if (s.clanType == clan) return s;
        return null;
    }

    /// <summary>Apply this config to all enemies in scene.</summary>
    public void ApplyToAllEnemies()
    {
        var enemies = Object.FindObjectsByType<EnemyDarkEnergy>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            var scale = GetScaleForClan(e.ClanType);
            if (scale != null)
                e.ApplyLevelScaling(scale.baseEnergy, scale.bondThreshold);
        }
        Debug.Log($"[LevelDarkEnergyConfig] Applied {levelName} to {enemies.Length} enemies");
    }
}