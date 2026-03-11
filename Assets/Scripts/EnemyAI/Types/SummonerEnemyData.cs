using UnityEngine;

[CreateAssetMenu(fileName = "SummonerEnemyData", menuName = "PlanetOfTwins/Enemy Data/Summoner")]
public class SummonerEnemyData : EnemyData
{
    [Header("Summoner — Spawn")]
    public int spawnCount = 2;
    public float spawnRadius = 5f;    // Local type: radius around self
    public float globalSpawnRadius = 15f;   // Global type: radius around player
    public float summonCooldown = 8f;
    public float summonRange = 6f;    // Triggers summon when player within this range
}
