using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum SummonerType { Local, Global }

public class SummonerEnemy : Enemy
{
    [Header("Summoner Config")]
    [SerializeField] private SummonerType summonerType = SummonerType.Local;
    [SerializeField] private GameObject spawnPrefab;
    [SerializeField] private EnemyData spawnData;
    [SerializeField] private int spawnCount = 2;
    [SerializeField] private float spawnRadius = 5f;
    [SerializeField] private float globalSpawnRadius = 15f;
    [SerializeField] private float summonCooldown = 8f;
    [SerializeField] private float summonRange = 6f;

    // Global summoner needs to know where players are
    // These are set by EnemyDetection — no longer needed for side assignment,
    // only used to find a spawn position near the player
    [Header("Global Summoner Only")]
    [SerializeField] private Transform leftTwinTransform;
    [SerializeField] private Transform rightTwinTransform;

    private float _lastSummonTime = -999f;
    private bool _isSummoning;

    protected override void InitStates()
    {
        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this);
        AttackState = new SummonState(this); // summoner "attacks" by summoning
        PossessedState = new PossessedState(this, possessedTargetLayer);
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is SummonerEnemyData summonerData)
        {
            spawnCount = summonerData.spawnCount;
            spawnRadius = summonerData.spawnRadius;
            globalSpawnRadius = summonerData.globalSpawnRadius;
            summonCooldown = summonerData.summonCooldown;
            summonRange = summonerData.summonRange;
        }
    }
    public bool CanSummon =>
        Time.time >= _lastSummonTime + summonCooldown && !_isSummoning;

    public void TriggerSummon()
    {
        if (!CanSummon) return;
        _lastSummonTime = Time.time;
        StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        _isSummoning = true;

        var spawner = FindAnyObjectByType<EnemySpawner>();

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = summonerType == SummonerType.Local
                ? GetLocalSpawnPosition()
                : GetGlobalSpawnPosition();

            if (spawnPos != Vector3.zero && spawner != null)
            {
                var entry = new SideTypeEntry
                {
                    prefab = spawnPrefab,
                    data = spawnData,
                    maxActiveOfType = 0,
                    weight = 1
                };

                // FIX: zone spawner no longer needs SpawnSide
                // SummonerSpawn uses active zone config — pass Left as dummy,
                // spawner ignores side for summoner-spawned enemies
                spawner.SummonerSpawn(entry, spawnPos);
            }

            yield return new WaitForSeconds(0.3f);
        }

        _isSummoning = false;
    }

    private Vector3 GetLocalSpawnPosition()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 rand2D = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(rand2D.x, 0f, rand2D.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return hit.position + Vector3.up * 1.08f; ;
        }
        return Vector3.zero;
    }

    private Vector3 GetGlobalSpawnPosition()
    {
        // Try both twins, return first valid position found
        Transform[] twins = { leftTwinTransform, rightTwinTransform };

        foreach (var twin in twins)
        {
            if (twin == null) continue;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 rand2D = Random.insideUnitCircle * globalSpawnRadius;
                Vector3 candidate = twin.position + new Vector3(rand2D.x, 0f, rand2D.y);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;

                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(hit.position, twin.position,
                        NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                    return hit.position + Vector3.up * 1.08f;
            }
        }
        return Vector3.zero;
    }
}