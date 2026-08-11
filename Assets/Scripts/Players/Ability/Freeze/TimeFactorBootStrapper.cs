using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TimeFactorBootstrapper : MonoBehaviour
{
    [SerializeField] private MonoBehaviour timeFactorRegistryObject;

    private ITimeFactorRegistry _registry;
    public ITimeFactorRegistry Registry => _registry;

    // Per-scene tracking so unload can remove exactly that scene's entries (3.8)
    private readonly Dictionary<Scene, List<ITimeAffected>> _byScene = new();

    private void Awake()
    {
        _registry = timeFactorRegistryObject as ITimeFactorRegistry;
        if (_registry == null)
            Debug.LogError("[TimeFactorBootstrapper] Missing ITimeFactorRegistry.");
    }

    private void Start()
    {
        ScanAll();
        SceneManager.sceneLoaded   += OnChunkLoaded;
        SceneManager.sceneUnloaded += OnChunkUnloaded; // 3.8: remove stale entries on unload
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded   -= OnChunkLoaded;
        SceneManager.sceneUnloaded -= OnChunkUnloaded;
    }

    private void ScanAll()
    {
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mb in all)
            if (mb is ITimeAffected affected)
                RegisterForScene(mb.gameObject.scene, affected);
    }

    private void OnChunkLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only scan scene-placed ITimeAffected in the newly loaded chunk.
        // Pool-spawned enemies register themselves via RegisterEntity when spawned.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
                if (mb is ITimeAffected affected)
                    RegisterForScene(scene, affected);
    }

    private void OnChunkUnloaded(Scene scene)
    {
        // Remove all scene-placed entries for the unloaded chunk (3.8).
        // Pool-spawned enemies were already unregistered by EnemySpawner.DespawnZone.
        if (!_byScene.TryGetValue(scene, out var list)) return;
        foreach (var affected in list)
            _registry?.Unregister(affected);
        _byScene.Remove(scene);
    }

    private void RegisterForScene(Scene scene, ITimeAffected affected)
    {
        _registry?.Register(affected);
        if (!_byScene.TryGetValue(scene, out var list))
        {
            list = new List<ITimeAffected>();
            _byScene[scene] = list;
        }
        if (!list.Contains(affected))
            list.Add(affected);
    }

    // Called by EnemySpawner when a pool-spawned enemy is activated / returned
    public void RegisterEntity(ITimeAffected entity) => _registry?.Register(entity);
    public void UnregisterEntity(ITimeAffected entity) => _registry?.Unregister(entity);
}
