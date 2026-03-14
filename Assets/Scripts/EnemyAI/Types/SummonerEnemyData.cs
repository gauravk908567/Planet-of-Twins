using UnityEngine;

/// <summary>
/// ScriptableObject for summoner enemies.
/// Inherits all ranged fields (attackRange, desiredRange, minEngageRange,
/// projectilePrefab, projectileSpeed, useProjectile) from RangedEnemyData.
/// Adds summoning behaviour fields.
///
/// CREATE: Right-click → Create → Enemy/SummonerEnemyData
/// </summary>
[CreateAssetMenu(menuName = "Enemy/SummonerEnemyData", fileName = "SummonerEnemyData")]
public class SummonerEnemyData : RangedEnemyData
{
    [Header("Summoning")]
    [Tooltip("Entry describing what minion prefab to spawn and with what weight/data.")]
    public SideTypeEntry summonEntry;

    [Tooltip("Time between summon casts. Should be large — summoner relies on minions.")]
    public float summonCooldown = 10f;

    [Tooltip("Maximum living minions from this summoner. Won't summon above this count.")]
    public int maxMinions = 3;

    [Tooltip("Short delay between the summon animation trigger and the actual spawn.")]
    public float summonSpawnDelay = 0.6f;
}