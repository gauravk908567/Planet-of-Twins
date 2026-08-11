using UnityEditor;
using UnityEngine;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// One-click wiring for the POI energy-feed ecology (idempotent — run any time):
    ///   1. Adds GOAPGoalSeekEnergy + GOAPActionSeekEnergy to every enemy prefab that has a GOAP
    ///      brain + EnemyDarkEnergy (skips SiphonGhost — a summon does not idle-visit POIs), and
    ///      assigns SeekEnergyUtilProfile to the goal.
    ///   2. Adds a PoiEnergyEmitter (with the default PoiEnergyProfile) to every POIBase in the
    ///      OPEN scenes that lacks one. Open the area scene(s) you want wired first.
    /// The feed cue book/id and per-POI profile overrides stay user authoring in the Inspector.
    /// </summary>
    public static class PoiEcologyAuthoring
    {
        private const string EnemyPrefabFolder = "Assets/Models/Prefabs/Enemies";
        private const string SeekProfilePath =
            "Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/Utility/Data/SeekEnergyUtilProfile.asset";
        private const string DefaultFeedProfilePath =
            "Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/POI/Data/DefaultPoiEnergyProfile.asset";

        // Menu retired (tool consolidation 2026-07-10) — invoked as a Fix from the Scene Health
        // Dashboard (Wiring recipe: POI-without-emitter; Enemy prefabs recipe: missing SeekEnergy).
        public static void Wire()
        {
            var seekProfile = AssetDatabase.LoadAssetAtPath<UtilityWeightProfile>(SeekProfilePath);
            if (seekProfile == null)
            {
                Debug.LogError($"[PoiEcology] Missing {SeekProfilePath} — reimport/compile first.");
                return;
            }

            int prefabsWired = WirePrefabs(seekProfile);
            int emittersAdded = WireScenePois(GetOrCreateFeedProfile());

            AssetDatabase.SaveAssets();
            Debug.Log($"[PoiEcology] Done — {prefabsWired} enemy prefab(s) wired with SeekEnergy, " +
                      $"{emittersAdded} PoiEnergyEmitter(s) added to POIs in the open scenes.");
        }

        private static int WirePrefabs(UtilityWeightProfile seekProfile)
        {
            int wired = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool eligible = root.GetComponent<PoTGOAPBrainBase>() != null
                                    && root.GetComponent<EnemyDarkEnergy>() != null
                                    && root.GetComponent<SiphonGhost>() == null;
                    if (!eligible) continue;

                    bool dirty = false;

                    var goal = root.GetComponent<GOAPGoalSeekEnergy>();
                    if (goal == null) { goal = root.AddComponent<GOAPGoalSeekEnergy>(); dirty = true; }

                    var so = new SerializedObject(goal);
                    var profileProp = so.FindProperty("_utilityProfile");
                    if (profileProp.objectReferenceValue == null)
                    {
                        profileProp.objectReferenceValue = seekProfile;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                    }

                    if (root.GetComponent<GOAPActionSeekEnergy>() == null)
                    { root.AddComponent<GOAPActionSeekEnergy>(); dirty = true; }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        wired++;
                        Debug.Log($"[PoiEcology] wired {path}");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return wired;
        }

        private static int WireScenePois(PoiEnergyProfile feedProfile)
        {
            int added = 0;
            foreach (var poi in Object.FindObjectsByType<POIBase>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (poi.GetComponent<PoiEnergyEmitter>() != null) continue;

                var emitter = Undo.AddComponent<PoiEnergyEmitter>(poi.gameObject);
                var so = new SerializedObject(emitter);
                so.FindProperty("_profile").objectReferenceValue = feedProfile;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(poi.gameObject);
                added++;
            }
            if (added > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            return added;
        }

        private static PoiEnergyProfile GetOrCreateFeedProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PoiEnergyProfile>(DefaultFeedProfilePath);
            if (profile != null) return profile;

            string dir = System.IO.Path.GetDirectoryName(DefaultFeedProfilePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace('\\', '/'),
                                           System.IO.Path.GetFileName(dir));

            profile = ScriptableObject.CreateInstance<PoiEnergyProfile>();
            AssetDatabase.CreateAsset(profile, DefaultFeedProfilePath);
            Debug.Log($"[PoiEcology] created {DefaultFeedProfilePath} (defaults — tune per POI by " +
                      "duplicating it and assigning the copy on that POI's emitter).");
            return profile;
        }
    }
}
