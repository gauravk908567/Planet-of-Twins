using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// Editor-only persisted store of "not required" waivers for Scene Health findings.
    /// A waiver marks a finding as reviewed-and-accepted: it drops out of the recipe's Pass/Warn/Fail
    /// status (so backdrop-area false positives stop colouring cells) but stays VISIBLE in the detail
    /// pane as a neutral info line with its reason.
    ///
    /// Key = scope + recipe/category + message, so a waiver survives re-scans and re-opens. A scene's scope
    /// is its asset GUID, not its path, so a waiver also survives moving or renaming the scene (game.md §20.5,
    /// silent-break class 9); project recipes use "PROJECT". Stored as JSON under ProjectSettings/
    /// (committable team knowledge — which findings the project has accepted — not a per-user pref). Each
    /// entry also carries the scene's current path, for people reading the JSON only: rewritten on every
    /// save, never read back.
    /// </summary>
    public static class SceneHealthWaivers
    {
        [System.Serializable] private class Entry { public string key; public string scene; public string reason; }
        [System.Serializable] private class Store { public List<Entry> entries = new List<Entry>(); }

        // Relative to the project root (Unity's working directory in the editor).
        private const string FilePath = "ProjectSettings/PoTSceneHealthWaivers.json";
        private const char Separator = '␟';
        private const string ProjectScope = "PROJECT";   // the dashboard's scope for project-wide recipes

        private static Dictionary<string, string> _map;
        private static Dictionary<string, string> Map { get { if (_map == null) Load(); return _map; } }

        /// <summary>Unit-separator (␟) delimited — will never appear in a GUID, recipe, or message.
        /// <paramref name="scope"/> is a scene path (stored as that scene's GUID) or "PROJECT".</summary>
        public static string Key(string scope, string recipe, string message)
            => $"{ScopeId(scope)}{Separator}{recipe}{Separator}{message}";

        public static bool IsWaived(string key) => Map.ContainsKey(key);
        public static string GetReason(string key) => Map.TryGetValue(key, out var r) ? r : "";
        public static int Count => Map.Count;

        /// <summary>Waivers whose scene no longer exists (deleted, or a key from before GUID scopes).
        /// Path Health reports them — an orphaned waiver silently re-flags nothing and hides nothing.</summary>
        public static List<string> OrphanedKeys()
        {
            var orphans = new List<string>();
            foreach (var key in Map.Keys)
            {
                string scope = ScopeOf(key);
                if (scope == ProjectScope) continue;
                string path = AssetDatabase.GUIDToAssetPath(scope);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) orphans.Add(key);
            }
            return orphans;
        }

        // A scene path becomes its GUID; "PROJECT" (or a path the AssetDatabase can't resolve) stays as given.
        private static string ScopeId(string scope)
        {
            if (string.IsNullOrEmpty(scope) || scope == ProjectScope) return scope;
            string guid = AssetDatabase.AssetPathToGUID(scope);
            return string.IsNullOrEmpty(guid) ? scope : guid;
        }

        private static string ScopeOf(string key)
        {
            int cut = key.IndexOf(Separator);
            return cut < 0 ? key : key.Substring(0, cut);
        }

        public static void SetWaived(string key, string reason)
        {
            Map[key] = reason ?? "";
            Save();
        }

        public static void Remove(string key)
        {
            if (Map.Remove(key)) Save();
        }

        private static void Load()
        {
            _map = new Dictionary<string, string>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var store = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath));
                    if (store?.entries != null)
                        foreach (var e in store.entries)
                            if (!string.IsNullOrEmpty(e.key)) _map[e.key] = e.reason ?? "";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneHealthWaivers] load failed ({FilePath}): {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                var store = new Store();
                foreach (var kv in _map)
                {
                    string scope = ScopeOf(kv.Key);
                    string scene = scope == ProjectScope ? "" : AssetDatabase.GUIDToAssetPath(scope);
                    store.entries.Add(new Entry { key = kv.Key, scene = scene, reason = kv.Value });
                }
                File.WriteAllText(FilePath, JsonUtility.ToJson(store, true));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneHealthWaivers] save failed ({FilePath}): {ex.Message}");
            }
        }
    }
}
