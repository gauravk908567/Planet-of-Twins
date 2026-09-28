using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// P8.1 (instruction.md) <c>Boot_PersistentLoadsBeforeAreas</c>: a fresh launch from Bootstrap through the real
/// front-end and intro loads Persistent before any area, and every Persistent manager's static Instance is already
/// set by then. Checked the moment Persistent finishes loading (its Awake/OnEnable done, no area load started yet)
/// and again when the first area has loaded.
/// </summary>
public class BootTests
{
    private List<int> _emptySlots;
    private readonly List<string> _loadOrder = new();
    private int _persistentIndex;
    private int _firstAreaIndex;
    private List<string> _missingAtPersistentLoad;
    private List<string> _missingAtFirstArea;

    [SetUp]
    public void SetUp()
    {
        _emptySlots = BootHarness.EmptySlots();
        _loadOrder.Clear();
        _persistentIndex = _firstAreaIndex = -1;
        _missingAtPersistentLoad = _missingAtFirstArea = null;
    }

    [TearDown]
    public void TearDown()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        BootHarness.DeleteSlotsCreatedSince(_emptySlots);
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator Boot_PersistentLoadsBeforeAreas()
    {
        if (_emptySlots.Count == 0) Assert.Ignore("All save slots hold saves; free one to run the boot tests.");

        SceneManager.sceneLoaded += HandleSceneLoaded;
        yield return BootHarness.BootNewGame(_emptySlots[0]);
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        string order = string.Join(" → ", _loadOrder);
        Assert.GreaterOrEqual(_persistentIndex, 0, $"Persistent never loaded. Load order: {order}");
        Assert.GreaterOrEqual(_firstAreaIndex, 0, $"No area loaded. Load order: {order}");
        Assert.Less(_persistentIndex, _firstAreaIndex, $"An area loaded before Persistent. Load order: {order}");
        Assert.IsEmpty(_missingAtPersistentLoad,
            "These managers had no Instance once Persistent had loaded (before any area): " +
            string.Join(", ", _missingAtPersistentLoad));
        Assert.IsEmpty(_missingAtFirstArea,
            "These managers had no Instance when the first area loaded: " + string.Join(", ", _missingAtFirstArea));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _loadOrder.Add(scene.name);
        if (_persistentIndex < 0 && IsPersistent(scene))
        {
            _persistentIndex = _loadOrder.Count - 1;
            _missingAtPersistentLoad = PersistentSingletons.MissingInstances();
        }
        else if (_firstAreaIndex < 0 && IsArea(scene))
        {
            _firstAreaIndex = _loadOrder.Count - 1;
            _missingAtFirstArea = PersistentSingletons.MissingInstances();
        }
    }

    // Persistent = the scene that holds the streaming manager (found by component, not by its Instance, so a
    // missing Instance is reported as such instead of as "Persistent never loaded").
    private static bool IsPersistent(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<SceneFlowManager>(true) != null) return true;
        return false;
    }

    private static bool IsArea(Scene scene) => scene.path.StartsWith(PoTPaths.Create.AreaScenes + "/");
}
