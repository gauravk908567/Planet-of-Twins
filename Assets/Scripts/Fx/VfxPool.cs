using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-prefab GameObject pool under FxPoolRoot (Persistent), mirroring <c>EnemyPool</c>. Instances are
/// reused, never destroyed at runtime, and reparented back to the pool root on return — so a follow
/// target that unloads with its area scene can never drag a pooled instance out of Persistent (F1).
/// </summary>
public class VfxPool
{
    private readonly Transform _root;
    private readonly Dictionary<Object, Queue<GameObject>> _pools = new Dictionary<Object, Queue<GameObject>>();

    public VfxPool(Transform root) { _root = root; }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        var q = GetQueue(prefab);
        for (int i = 0; i < count; i++) q.Enqueue(Create(prefab));
    }

    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;
        var q = GetQueue(prefab);
        var inst = q.Count > 0 ? q.Dequeue() : Create(prefab);
        inst.transform.SetParent(_root, false);
        inst.SetActive(true);
        return inst;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;
        instance.SetActive(false);
        instance.transform.SetParent(_root, false);
        if (prefab != null) GetQueue(prefab).Enqueue(instance);
    }

    private Queue<GameObject> GetQueue(Object prefab)
    {
        if (!_pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            _pools[prefab] = q;
        }
        return q;
    }

    private GameObject Create(GameObject prefab)
    {
        var inst = Object.Instantiate(prefab, _root);
        inst.SetActive(false);
        return inst;
    }
}
