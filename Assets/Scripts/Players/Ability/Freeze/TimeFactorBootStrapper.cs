using UnityEngine;

public class TimeFactorBootstrapper : MonoBehaviour
{
    [SerializeField] private MonoBehaviour timeFactorRegistryObject;

    private ITimeFactorRegistry _registry;
    public ITimeFactorRegistry Registry => _registry;

    private void Awake()
    {
        _registry = timeFactorRegistryObject as ITimeFactorRegistry;
        if (_registry == null)
            Debug.LogError("[TimeFactorBootstrapper] Missing ITimeFactorRegistry.");
    }

    private void Start()
    {
        // Find all ITimeAffected MonoBehaviours in scene (players, soul)
        var all = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var mb in all)
        {
            if (mb is ITimeAffected affected)
            {
                _registry.Register(affected);
               // Debug.Log($"[TimeFactorBootstrapper] Registered {mb.gameObject.name}");
            }
        }
    }

    // Called by EnemySpawner when a new enemy is spawned
    public void RegisterEntity(ITimeAffected entity) => _registry?.Register(entity);
    public void UnregisterEntity(ITimeAffected entity) => _registry?.Unregister(entity);
}