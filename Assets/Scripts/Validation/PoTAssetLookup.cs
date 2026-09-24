#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Folder-free asset lookup for editor tools (folder restructure Stage 0, game.md §20.5). A tool that searched a
/// hard-coded folder went quietly EMPTY once that folder moved — FindAssets over a missing folder returns nothing and
/// LoadAssetAtPath returns null. These search all of Assets/ by type, or by type + exact file name, so a move changes
/// nothing, and a missing or duplicated asset is a loud LogError, never a silent null. Names live in
/// <see cref="PoTPaths.Named"/>.
/// Editor-only, but outside an Editor folder so runtime-assembly editor code (<c>#if UNITY_EDITOR</c>, e.g. the
/// GameDebuggerV2 auto-fill) can use it too.
/// </summary>
public static class PoTAssetLookup
{
    private static readonly string[] AssetsRoot = { "Assets" };   // project content only — never Packages/

    /// <summary>Paths of every asset whose MAIN type is <paramref name="type"/> (or a subclass) and whose file name,
    /// without extension, is exactly <paramref name="name"/>. Silent — the caller decides what 0 or 2+ means.</summary>
    public static List<string> PathsOf(string name, System.Type type)
    {
        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets(name, AssetsRoot))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            var main = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (main != null && type.IsAssignableFrom(main)) paths.Add(path);
        }
        return paths;
    }

    public static List<string> PathsOf<T>(string name) where T : Object => PathsOf(name, typeof(T));

    /// <summary>Path of the ONE <typeparamref name="T"/> named <paramref name="name"/>. LogError + null when there is
    /// none (renamed or deleted) or more than one (an ambiguous copy).</summary>
    public static string FindUniquePath<T>(string name) where T : Object
    {
        var paths = PathsOf<T>(name);
        if (paths.Count == 1) return paths[0];
        if (paths.Count == 0)
            Debug.LogError($"[PoTAssetLookup] No {typeof(T).Name} named '{name}' under Assets/ — renamed or deleted? " +
                           "Update PoTPaths.Named, then run Planet of Twins Tools ▸ Validation ▸ Path Health.");
        else
            Debug.LogError($"[PoTAssetLookup] {paths.Count} {typeof(T).Name} assets named '{name}' — ambiguous: " +
                           string.Join(", ", paths) + ". Rename or remove the extra copy.");
        return null;
    }

    /// <summary><see cref="FindUniquePath{T}"/>, loaded. Null (already logged) when missing or ambiguous.</summary>
    public static T FindUnique<T>(string name) where T : Object
    {
        string path = FindUniquePath<T>(name);
        return path != null ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
    }

    /// <summary>Every prefab under Assets/ whose ROOT carries a <typeparamref name="T"/> (or a subclass), in path
    /// order. A dependency pre-filter (the prefab must depend on T's script or a subclass's) skips the rest without
    /// loading them, so a whole-project scan stays cheap.</summary>
    public static List<GameObject> PrefabsWith<T>() where T : Component
    {
        var scripts = ScriptPathsFor(typeof(T));
        var result = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", AssetsRoot))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.GetDependencies(path, true).Any(scripts.Contains)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<T>() != null) result.Add(prefab);
        }
        return result;
    }

    // The script paths of T and every subclass — the MonoScripts a prefab depends on when it carries one.
    private static HashSet<string> ScriptPathsFor(System.Type type)
    {
        var set = new HashSet<string>();
        foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
        {
            var cls = script.GetClass();
            if (cls != null && type.IsAssignableFrom(cls)) set.Add(AssetDatabase.GetAssetPath(script));
        }
        return set;
    }

    /// <summary>Creates <paramref name="folder"/> and any missing parents (e.g. a <see cref="PoTPaths.Create"/> root).</summary>
    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
