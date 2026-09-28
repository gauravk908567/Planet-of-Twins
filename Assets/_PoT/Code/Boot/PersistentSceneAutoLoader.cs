#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

/// Loads Persistent.unity additively when entering Play Mode directly from an
/// area scene in the Editor, so all managers exist. No-op in builds and no-op
/// when Persistent is already loaded (Bootstrap path).
public static class PersistentSceneAutoLoader
{
    private const string PersistentScene = "Persistent";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsurePersistent()
    {
        if (SceneManager.GetSceneByName(PersistentScene).isLoaded) return;
        var active = SceneManager.GetActiveScene().name;
        if (active == "Bootstrap" || active == PersistentScene) return; // boot path handles it
        if (active.StartsWith("InitTestScene")) return;   // the Test Runner's scene: PlayMode tests boot from Bootstrap
        SceneManager.LoadScene(PersistentScene, LoadSceneMode.Additive);
        PoTLog.Streaming?.Info("Loaded Persistent additively for editor play.");
    }
}
#endif
