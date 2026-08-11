using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// Tools ▸ Planet of Twins ▸ Area Tools — the consolidated area authoring window
    /// (2026-07-10 tool merge of New Area Scene… + Area Setup).
    ///
    /// Tab 1 · New Area: scaffolds a streamable area scene with the FULL kit (P14):
    /// folder-per-scene, WorldLocationSO, AreaZoneConfig (+ sub-SOs), SpawnZone,
    /// LocationEntrance, SceneLoadTrigger, optional QTESceneAnchor, area identity volume
    /// (global, priority 10). Area scenes do NOT get their own camera / AudioListener
    /// (Persistent owns those, R9); they DO get their own lighting.
    ///
    /// Tab 2 · Setup Zone: operates on a SpawnZone in the active scene — creates/repairs its
    /// AreaZoneConfig + 3 sub-SOs, and auto-populates left/right spawn-point Transforms from
    /// named child containers (or Tag / Manual-parent). POIs are NOT wired here — they
    /// self-register to POIManager at runtime. All writes go through SerializedObject
    /// (Undo-able); nothing cross-scene is ever written (R2-safe).
    ///
    /// Both tabs hand off to the Scene Health Dashboard for verification.
    /// </summary>
    public class AreaToolsWindow : EditorWindow
    {
        private int _tab;
        private static readonly string[] Tabs = { "New Area", "Setup Zone" };

        [MenuItem("Tools/Planet of Twins/Area Tools")]
        public static void Open()
        {
            var w = GetWindow<AreaToolsWindow>("PoT Area Tools");
            w.minSize = new Vector2(460, 480);
            w.AutoPickZone();
        }

        private void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, Tabs);
            EditorGUILayout.Space();
            if (_tab == 0) DrawNewAreaTab();
            else DrawSetupZoneTab();
        }

        // ═══════════════ Tab 1 · New Area (was NewAreaSceneWindow) ═══════════════
        private string _name = "L_NewArea";
        private bool _addSpawnZone = true;
        private bool _addZoneConfig = true;
        private bool _addEntrance = true;
        private bool _addLoadTrigger = true;
        private bool _addQteAnchor = false;
        private bool _addIdentityVolume = true;
        private bool _addDirectionalLight = true;
        private bool _addToBuildSettings = true;

        private void DrawNewAreaTab()
        {
            EditorGUILayout.LabelField("New Area Scene — full kit (P14)", EditorStyles.boldLabel);
            _name = EditorGUILayout.TextField("Area name", _name);
            _addSpawnZone = EditorGUILayout.Toggle("SpawnZone skeleton", _addSpawnZone);
            using (new EditorGUI.DisabledScope(!_addSpawnZone))
                _addZoneConfig = EditorGUILayout.Toggle("  + AreaZoneConfig (+sub-SOs)", _addZoneConfig);
            _addEntrance = EditorGUILayout.Toggle("Default LocationEntrance", _addEntrance);
            _addLoadTrigger = EditorGUILayout.Toggle("SceneLoadTrigger (→ this area)", _addLoadTrigger);
            _addQteAnchor = EditorGUILayout.Toggle("QTESceneAnchor (empty)", _addQteAnchor);
            _addIdentityVolume = EditorGUILayout.Toggle("Identity volume (global, prio 10)", _addIdentityVolume);
            _addDirectionalLight = EditorGUILayout.Toggle("Directional Light", _addDirectionalLight);
            _addToBuildSettings = EditorGUILayout.Toggle("Add to Build Settings", _addToBuildSettings);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_name)))
                if (GUILayout.Button("Create Area Scene", GUILayout.Height(28)))
                    Create();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Creates Assets/Scenes/<name>/<name>.unity + WorldLocationSO (+ AreaZoneConfig kit). " +
                "After: build geometry, bake the NavMesh, set adjacency on the WorldLocationSO, place spawn " +
                "points/POIs, assign the identity volume's profile — then run the Scene Health Dashboard.", MessageType.Info);
        }

        private void Create()
        {
            string name = _name.Trim();
            string dir = $"Assets/Scenes/{name}";
            EnsureFolder(dir);
            string scenePath = $"{dir}/{name}.unity";
            if (System.IO.File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("Already exists", $"{scenePath} already exists.", "OK");
                return;
            }

            var checklist = new List<string>();

            // 1) The WorldLocationSO FIRST — scene objects (SceneLoadTrigger) reference it (asset refs are legal, R2).
            var loc = ScriptableObject.CreateInstance<WorldLocationSO>();
            var locSo = new SerializedObject(loc);
            locSo.FindProperty("scene").FindPropertyRelative("_name").stringValue = name;
            locSo.ApplyModifiedPropertiesWithoutUndo();
            string locPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/Location_{name}.asset");
            AssetDatabase.CreateAsset(loc, locPath);

            // 2) New empty scene ADDITIVELY so we never disturb the user's open scenes.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            if (_addDirectionalLight)
            {
                var sun = new GameObject("Directional Light");
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Move(sun, scene);
            }

            CreateRoot("Geometry", scene);
            CreateRoot("POIs", scene);

            // AreaSpawnPoints + L/R children, wired.
            var aspGo = new GameObject("AreaSpawnPoints");
            Move(aspGo, scene);
            var asp = aspGo.AddComponent<AreaSpawnPoints>();
            var l = new GameObject("SpawnLeft").transform; l.SetParent(aspGo.transform); l.localPosition = new Vector3(-2, 0, 0);
            var r = new GameObject("SpawnRight").transform; r.SetParent(aspGo.transform); r.localPosition = new Vector3(2, 0, 0);
            asp.leftStart = l; asp.rightStart = r;

            // NavMeshSurface via reflection (package type — avoid a hard asmref).
            var navType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (navType != null)
            {
                var navGo = new GameObject("NavMeshSurface");
                navGo.AddComponent(navType);
                Move(navGo, scene);
                checklist.Add("Bake the NavMesh once geometry exists.");
            }
            else checklist.Add("NavMeshSurface type not found — add an AI Navigation surface manually.");

            if (_addSpawnZone)
            {
                var zoneGo = new GameObject("SpawnZone");
                Move(zoneGo, scene);
                var box = zoneGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(20, 5, 20);
                var zone = zoneGo.AddComponent<SpawnZone>();
                new GameObject("LeftSpawnPoints").transform.SetParent(zoneGo.transform);
                new GameObject("RightSpawnPoints").transform.SetParent(zoneGo.transform);

                if (_addZoneConfig)
                {
                    zone.areaConfig = CreateZoneConfig(dir, name);
                    checklist.Add("Fill the AreaZoneConfig sub-SOs (enemy roster, timings) + hand-place spawn point transforms.");
                }
                else checklist.Add("Use the Setup Zone tab to create this SpawnZone's AreaZoneConfig + populate points.");
            }

            if (_addEntrance)
            {
                var entrance = new GameObject("LocationEntrance_Default");
                Move(entrance, scene);
                entrance.AddComponent<LocationEntrance>();   // comesFrom = null → the default entrance
                checklist.Add("Position LocationEntrance_Default (+ add per-direction entrances with comesFrom set).");
            }

            if (_addLoadTrigger)
            {
                var trigGo = new GameObject($"SceneLoadTrigger_to_{name}");
                Move(trigGo, scene);
                var col = trigGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(6, 3, 2);
                var trig = trigGo.AddComponent<SceneLoadTrigger>();
                var trigSo = new SerializedObject(trig);
                trigSo.FindProperty("targetLocation").objectReferenceValue = loc;
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                    trigSo.FindProperty("playerLayer").intValue = 1 << playerLayer;
                else
                    checklist.Add("No 'Player' layer found — set SceneLoadTrigger.playerLayer manually.");
                trigSo.ApplyModifiedPropertiesWithoutUndo();
                checklist.Add("Move the SceneLoadTrigger to the area boundary (fully INSIDE this area) + set comesFrom.");
            }

            if (_addQteAnchor)
            {
                var anchor = new GameObject("QTESceneAnchor");
                Move(anchor, scene);
                anchor.AddComponent<QTESceneAnchor>();
                checklist.Add("Wire the QTESceneAnchor: definition SO, 2 trigger points, zone trigger, world-UI refs.");
            }

            if (_addIdentityVolume)
            {
                var volGo = new GameObject($"IdentityVolume_{name}");
                Move(volGo, scene);
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 10f; // the area identity layer — hue/temp only
                checklist.Add("Assign the identity volume's profile (hue/temp only — the story grade owns the rest).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            if (_addToBuildSettings) AddToBuildSettings(scenePath);
            else checklist.Add("Scene NOT in Build Settings — add it for streaming (SceneReference needs it).");

            checklist.Add("Set adjacency + isStartLocation on the new WorldLocationSO.");
            checklist.Add("Run Tools ▸ Planet of Twins ▸ Scene Health Dashboard to verify the kit.");

            EditorGUIUtility.PingObject(loc);
            Debug.Log($"[New Area] Created {scenePath} + {locPath}.\nTODO:\n - " + string.Join("\n - ", checklist), loc);
        }

        // ── AreaZoneConfig + sub-SOs (the dashboard's config-chain checks expect these) ──
        private static AreaZoneConfig CreateZoneConfig(string dir, string area)
        {
            var cfg = ScriptableObject.CreateInstance<AreaZoneConfig>();
            AssetDatabase.CreateAsset(cfg, AssetDatabase.GenerateUniqueAssetPath($"{dir}/ZoneConfig_{area}.asset"));

            var so = new SerializedObject(cfg);
            so.FindProperty("spawnSetup").objectReferenceValue = CreateSub<SpawnSetupConfig>(dir, "SpawnSetup", area);
            so.FindProperty("environment").objectReferenceValue = CreateSub<ZoneEnvironmentConfig>(dir, "Environment", area);
            so.FindProperty("groupSpawn").objectReferenceValue = CreateSub<GroupSpawnConfig>(dir, "GroupSpawn", area);
            so.ApplyModifiedPropertiesWithoutUndo();
            return cfg;
        }

        private static T CreateSub<T>(string dir, string prefix, string area) where T : ScriptableObject
        {
            var sub = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(sub, AssetDatabase.GenerateUniqueAssetPath($"{dir}/{prefix}_{area}.asset"));
            return sub;
        }

        // ═══════════════ Tab 2 · Setup Zone (was AreaAutoWireWindow) ═══════════════
        private const string ConfigRoot = "Assets/Scripts/SpawnSystem/SpawnArea";

        private SpawnZone _zone;
        private Vector2 _scroll;

        // left/right Transform source
        private enum TransformSource { ChildContainer, Tag, ManualParent }
        private TransformSource _leftSource = TransformSource.ChildContainer, _rightSource = TransformSource.ChildContainer;
        private string _leftContainer = "LeftSpawnPoints", _rightContainer = "RightSpawnPoints";
        private string _leftTag = "Respawn", _rightTag = "Respawn";
        private Transform _leftParent, _rightParent;

        private string _status = "";

        private void DrawSetupZoneTab()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _zone = (SpawnZone)EditorGUILayout.ObjectField("SpawnZone", _zone, typeof(SpawnZone), true);
                if (GUILayout.Button("Find in scene", GUILayout.Width(100))) AutoPickZone();
            }

            if (_zone == null)
            {
                EditorGUILayout.HelpBox("Select a SpawnZone (or open an area scene that has one and press 'Find in scene').", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            DrawConfigSection();
            EditorGUILayout.Space();
            DrawPopulateSection();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        // ── config ────────────────────────────────────────────────────────────────
        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("1 · Config (AreaZoneConfig + sub-SOs)", EditorStyles.boldLabel);
            var cfg = _zone.areaConfig;
            if (cfg == null)
            {
                EditorGUILayout.HelpBox("This zone has no AreaZoneConfig.", MessageType.Warning);
                if (GUILayout.Button("Create AreaZoneConfig + sub-SOs"))
                    CreateConfig();
            }
            else
            {
                EditorGUILayout.ObjectField("AreaZoneConfig", cfg, typeof(AreaZoneConfig), false);
                bool missing = cfg.spawnSetup == null || cfg.environment == null || cfg.groupSpawn == null;
                if (missing)
                {
                    EditorGUILayout.HelpBox("Missing sub-SO(s): " +
                        (cfg.spawnSetup == null ? "spawnSetup " : "") +
                        (cfg.environment == null ? "environment " : "") +
                        (cfg.groupSpawn == null ? "groupSpawn" : ""), MessageType.Warning);
                    if (GUILayout.Button("Fill missing sub-SOs")) FillMissingSubSos(cfg);
                }
                else EditorGUILayout.LabelField("All 3 sub-SOs assigned ✔", EditorStyles.miniLabel);
            }
        }

        private void CreateConfig()
        {
            string sceneName = _zone.gameObject.scene.name;
            string dir = $"{ConfigRoot}/{sceneName}";
            EnsureFolder(dir);

            var setup = CreateSo<SpawnSetupConfig>($"{dir}/SpawnSetup_{sceneName}.asset");
            var env = CreateSo<ZoneEnvironmentConfig>($"{dir}/Environment_{sceneName}.asset");
            var grp = CreateSo<GroupSpawnConfig>($"{dir}/GroupSpawn_{sceneName}.asset");

            var cfg = ScriptableObject.CreateInstance<AreaZoneConfig>();
            cfg.areaName = sceneName;
            cfg.spawnSetup = setup;
            cfg.environment = env;
            cfg.groupSpawn = grp;
            string cfgPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/AreaZoneConfig_{sceneName}.asset");
            AssetDatabase.CreateAsset(cfg, cfgPath);
            AssetDatabase.SaveAssets();

            AssignAreaConfig(cfg);
            _status = $"Created config set in {dir} and assigned to the zone.";
        }

        private void FillMissingSubSos(AreaZoneConfig cfg)
        {
            string sceneName = _zone.gameObject.scene.name;
            string dir = $"{ConfigRoot}/{sceneName}";
            EnsureFolder(dir);

            var so = new SerializedObject(cfg);
            if (cfg.spawnSetup == null)
                so.FindProperty("spawnSetup").objectReferenceValue = CreateSo<SpawnSetupConfig>($"{dir}/SpawnSetup_{sceneName}.asset");
            if (cfg.environment == null)
                so.FindProperty("environment").objectReferenceValue = CreateSo<ZoneEnvironmentConfig>($"{dir}/Environment_{sceneName}.asset");
            if (cfg.groupSpawn == null)
                so.FindProperty("groupSpawn").objectReferenceValue = CreateSo<GroupSpawnConfig>($"{dir}/GroupSpawn_{sceneName}.asset");
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            _status = "Filled missing sub-SOs.";
        }

        private void AssignAreaConfig(AreaZoneConfig cfg)
        {
            var so = new SerializedObject(_zone);
            so.FindProperty("areaConfig").objectReferenceValue = cfg;
            so.ApplyModifiedProperties();
            EditorSceneMarkDirty();
        }

        // ── populate ──────────────────────────────────────────────────────────────
        private void DrawPopulateSection()
        {
            EditorGUILayout.LabelField("2 · Populate scene refs", EditorStyles.boldLabel);

            // left / right transforms
            EditorGUILayout.LabelField("Left spawn points", EditorStyles.miniBoldLabel);
            _leftSource = (TransformSource)EditorGUILayout.EnumPopup("Source", _leftSource);
            DrawTransformSourceFields(_leftSource, ref _leftContainer, ref _leftTag, ref _leftParent);

            EditorGUILayout.LabelField("Right spawn points", EditorStyles.miniBoldLabel);
            _rightSource = (TransformSource)EditorGUILayout.EnumPopup("Source", _rightSource);
            DrawTransformSourceFields(_rightSource, ref _rightContainer, ref _rightTag, ref _rightParent);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("POIs (spawn points / ritual sites / barriers) are NOT wired here — they " +
                "self-register to POIManager at runtime; the zone discovers the ones inside its collider.",
                MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("Auto-Populate Left/Right", GUILayout.Height(28)))
                PopulateAll();
        }

        private void DrawTransformSourceFields(TransformSource src, ref string container, ref string tag, ref Transform parent)
        {
            switch (src)
            {
                case TransformSource.ChildContainer: container = EditorGUILayout.TextField("Container child name", container); break;
                case TransformSource.Tag: tag = EditorGUILayout.TagField("Tag", tag); break;
                case TransformSource.ManualParent: parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true); break;
            }
        }

        private void PopulateAll()
        {
            var scene = _zone.gameObject.scene;

            var left = CollectTransforms(_leftSource, _leftContainer, _leftTag, _leftParent, scene);
            var right = CollectTransforms(_rightSource, _rightContainer, _rightTag, _rightParent, scene);

            var so = new SerializedObject(_zone);
            WriteArray(so.FindProperty("leftSpawnPoints"), left);
            WriteArray(so.FindProperty("rightSpawnPoints"), right);
            so.ApplyModifiedProperties();
            EditorSceneMarkDirty();

            // POIs (spawn points / ritual sites / barriers) are no longer wired on the zone — they
            // self-register to POIManager at runtime and the zone discovers the ones inside its collider.
            _status = $"Populated — left:{left.Count}  right:{right.Count}  "
                    + "(POIs auto-register at runtime — no longer wired on the zone).";
        }

        // ── collection ──────────────────────────────────────────────────────────
        private List<Object> CollectTransforms(TransformSource src, string container, string tag, Transform parent, Scene scene)
        {
            var list = new List<Object>();
            switch (src)
            {
                case TransformSource.ChildContainer:
                    var c = _zone.transform.Find(container);
                    if (c != null) foreach (Transform t in c) list.Add(t);
                    break;
                case TransformSource.Tag:
                    if (!string.IsNullOrEmpty(tag))
                        foreach (var go in GameObject.FindGameObjectsWithTag(tag))
                            if (go.scene == scene) list.Add(go.transform);
                    break;
                case TransformSource.ManualParent:
                    if (parent != null) foreach (Transform t in parent) list.Add(t);
                    break;
            }
            return list;
        }

        private static void WriteArray(SerializedProperty arrayProp, List<Object> items)
        {
            if (arrayProp == null) return;
            arrayProp.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ── shared helpers ──────────────────────────────────────────────────────
        private void AutoPickZone()
        {
            if (Selection.activeGameObject != null)
            {
                var sel = Selection.activeGameObject.GetComponentInParent<SpawnZone>();
                if (sel != null) { _zone = sel; return; }
            }
            var active = SceneManager.GetActiveScene();
            foreach (var z in Object.FindObjectsByType<SpawnZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (z.gameObject.scene == active) { _zone = z; return; }
        }

        private static T CreateSo<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, AssetDatabase.GenerateUniqueAssetPath(path));
            return so;
        }

        private static void CreateRoot(string n, Scene scene) => Move(new GameObject(n), scene);

        private static void Move(GameObject go, Scene scene) => SceneManager.MoveGameObjectToScene(go, scene);

        private static void AddToBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == scenePath) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private void EditorSceneMarkDirty()
        {
            if (_zone != null)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_zone.gameObject.scene);
        }
    }
}
