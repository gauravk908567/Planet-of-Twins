using UnityEngine;

public interface IEnemyPoolProvider
{
    GameObject Get(GameObject prefab);
    void Return(GameObject prefab, GameObject instance);
}