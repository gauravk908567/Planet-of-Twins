#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Dev-only runtime integrity canary (Editor + Development builds — compiled out of release).
/// On every scene load it re-checks the things that silently break this project at runtime:
///   • <c>[RequiredReference]</c> fields left null,
///   • duplicate singletons (the Restart→Bootstrap / DontDestroyOnLoad canary, R3).
/// Fail-LOUD via Debug.LogError (CLAUDE.md #4); never throws, no gameplay effect.
///
/// Cross-scene serialized refs (R2) are an *editor-time* check (they serialize as null at runtime),
/// so the editor Validator owns that one — this guard catches what the editor pass can miss.
/// </summary>
public static class SceneIntegrityChecker
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckRequiredRefs(scene);
        CheckDuplicateSingletons();
    }

    private static void CheckRequiredRefs(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var c in comps)
            {
                if (c == null) continue;
                var type = c.GetType();
                for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.GetCustomAttribute<RequiredReferenceAttribute>() == null) continue;
                        var v = f.GetValue(c);
                        if (v == null || (v is Object uo && uo == null))
                            Debug.LogError($"[Integrity] {type.Name}.{f.Name} is required but null " +
                                           $"(scene '{scene.name}').", c);
                    }
                }
            }
        }
    }

    private static void CheckDuplicateSingletons()
    {
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var seen = new Dictionary<System.Type, MonoBehaviour>();
        var flagged = new HashSet<System.Type>();
        foreach (var mb in all)
        {
            if (mb == null) continue;
            var t = mb.GetType();
            if (flagged.Contains(t)) continue;
            if (seen.ContainsKey(t))
            {
                if (HasStaticInstance(t))
                {
                    Debug.LogError($"[Integrity] Duplicate '{t.Name}' in loaded scenes — singleton duplicate " +
                                   "canary (R3). Check for DontDestroyOnLoad / stale statics / a manager " +
                                   "placed in an area scene instead of Persistent.", mb);
                    flagged.Add(t);
                }
            }
            else seen[t] = mb;
        }
    }

    private static bool HasStaticInstance(System.Type t)
    {
        const BindingFlags F = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        return t.GetProperty("Instance", F) != null || t.GetField("Instance", F) != null;
    }
}
#endif
