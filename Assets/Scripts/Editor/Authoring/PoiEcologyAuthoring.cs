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
        // Menu retired (tool consolidation 2026-07-10) — invoked as a Fix from the Scene Health
        // Dashboard (Wiring recipe: POI-without-emitter; Enemy prefabs recipe: missing SeekEnergy).
        public static void Wire()
        {
            var seekProfile = PoTAssetLookup.FindUnique<UtilityWeightProfile>(PoTPaths.Named.SeekEnergyUtilProfile);
            if (seekProfile == null) return;   // PoTAssetLookup logged why
            var feedProfile = GetOrCreateFeedProfile();
            if (feedProfile == null) return;

            int prefabsWired = WirePrefabs(seekProfile);
            int emittersAdded = WireScenePois(feedProfile);

            AssetDatabase.SaveAssets();
            Debug.Log($"[PoiEcology] Done — {prefabsWired} enemy prefab(s) wired with SeekEnergy, " +
                      $"{emittersAdded} PoiEnergyEmitter(s) added to POIs in the open scenes.");
        }

        private static int WirePrefabs(UtilityWeightProfile seekProfile)
        {
            int wired = 0;
            // Folder-free: every prefab with a GOAP brain, wherever it lives. Eligibility is read off the prefab
            // asset's root (same components as the loaded contents), so only eligible prefabs are opened.
            foreach (var prefab in PoTAssetLookup.PrefabsWith<PoTGOAPBrainBase>())
            {
                bool eligible = prefab.GetComponent<EnemyDarkEnergy>() != null
                                && prefab.GetComponent<SiphonGhost>() == null;
                if (!eligible) continue;

                string path = AssetDatabase.GetAssetPath(prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
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
            // Found by name in any folder; null (logged) if duplicated. Created only when none exists.
            string name = PoTPaths.Named.DefaultPoiEnergyProfile;
            if (PoTAssetLookup.PathsOf<PoiEnergyProfile>(name).Count > 0)
                return PoTAssetLookup.FindUnique<PoiEnergyProfile>(name);

            PoTAssetLookup.EnsureFolder(PoTPaths.Create.PoiEnergyProfiles);
            string path = $"{PoTPaths.Create.PoiEnergyProfiles}/{name}.asset";
            var profile = ScriptableObject.CreateInstance<PoiEnergyProfile>();
            AssetDatabase.CreateAsset(profile, path);
            Debug.Log($"[PoiEcology] created {path} (defaults — tune per POI by " +
                      "duplicating it and assigning the copy on that POI's emitter).");
            return profile;
        }
    }
}
