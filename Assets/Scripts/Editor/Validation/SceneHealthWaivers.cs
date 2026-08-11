using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// Editor-only persisted store of "not required" waivers for Scene Health findings.
    /// A waiver marks a finding as reviewed-and-accepted: it drops out of the recipe's Pass/Warn/Fail
    /// status (so backdrop-area false positives stop colouring cells) but stays VISIBLE in the detail
    /// pane as a neutral info line with its reason.
    ///
    /// Key = scope (scene path, or "PROJECT" for project recipes) + recipe/category + message, so a
    /// waiver survives re-scans and re-opens. Stored as JSON under ProjectSettings/ (committable team
    /// knowledge — which findings the project has accepted — not a per-user pref).
    /// </summary>
    public static class SceneHealthWaivers
    {
        [System.Serializable] private class Entry { public string key; public string reason; }
        [System.Serializable] private class Store { public List<Entry> entries = new List<Entry>(); }

        // Relative to the project root (Unity's working directory in the editor).
        private const string FilePath = "ProjectSettings/PoTSceneHealthWaivers.json";

        private static Dictionary<string, string> _map;
        private static Dictionary<string, string> Map { get { if (_map == null) Load(); return _map; } }

        /// <summary>Unit-separator (␟) delimited — will never appear in a scene path, recipe, or message.</summary>
        public static string Key(string scope, string recipe, string message)
            => $"{scope}␟{recipe}␟{message}";

        public static bool IsWaived(string key) => Map.ContainsKey(key);
        public static string GetReason(string key) => Map.TryGetValue(key, out var r) ? r : "";
        public static int Count => Map.Count;

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
                foreach (var kv in _map) store.entries.Add(new Entry { key = kv.Key, reason = kv.Value });
                File.WriteAllText(FilePath, JsonUtility.ToJson(store, true));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SceneHealthWaivers] save failed ({FilePath}): {ex.Message}");
            }
        }
    }
}
