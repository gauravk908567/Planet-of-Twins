using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : MonoBehaviour, IEnemyPoolProvider
{
    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        [Tooltip("How many instances to pre-create at scene load")]
        public int preWarmCount = 5;
    }

    [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();
    [SerializeField] private Transform poolParent;  // keep hierarchy clean

    private Transform _leftTwin;
    private Transform _rightTwin;

    private readonly Dictionary<GameObject, Queue<GameObject>> _pools
        = new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        if (poolParent == null)
            poolParent = transform;

        foreach (var config in poolConfigs)
            PreWarm(config.prefab, config.preWarmCount);
    }

    private void PreWarm(GameObject prefab, int count)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        for (int i = 0; i < count; i++)
        {
            var instance = CreateInstance(prefab);
            _pools[prefab].Enqueue(instance);
        }
    }

    public void SetTwinReferences(Transform left, Transform right)
    {
        _leftTwin = left;
        _rightTwin = right;
    }

    // IEnemyPoolProvider
    public GameObject Get(GameObject prefab)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        GameObject instance = _pools[prefab].Count > 0
            ? _pools[prefab].Dequeue()
            : CreateInstance(prefab);

        // FIX: disable NavMeshAgent before activating so it doesn't
        // warp to nearest NavMesh on Enable before position is set
        var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        instance.SetActive(true);

        // Position is set by EnemySpawner AFTER this returns
        // Agent re-enabled in EnemySpawner after position is set
        return instance;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;

        // Reset enemy state before returning to pool
        var enemy = instance.GetComponent<Enemy>();
        enemy?.ResetForPool();

        instance.SetActive(false);
        instance.transform.SetParent(poolParent);

        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<GameObject>();

        _pools[prefab].Enqueue(instance);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        var instance = Instantiate(prefab, poolParent);
        instance.SetActive(false);
        return instance;
    }
}