using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace PlanetOfTwins.EditorTools
{
    public enum RecipeStatus { Pass, Warn, Fail, NotApplicable }

    /// <summary>One recipe's outcome for one scene (or for the project). Status = worst finding.</summary>
    public sealed class RecipeResult
    {
        public string Recipe;
        public string Summary;                      // the "N placed, M wired" line shown in the cell
        public bool NotApplicable;
        public readonly List<ValidationFinding> Findings = new List<ValidationFinding>();

        public RecipeStatus Status
        {
            get
            {
                if (NotApplicable) return RecipeStatus.NotApplicable;
                // Waived ("not required") findings are excluded — a recipe whose only issues are waived reads green.
                if (Findings.Any(f => !f.Waived && f.Severity == ValidationSeverity.Error)) return RecipeStatus.Fail;
                if (Findings.Any(f => !f.Waived && f.Severity == ValidationSeverity.Warning)) return RecipeStatus.Warn;
                return RecipeStatus.Pass;
            }
        }

        public RecipeResult(string recipe) { Recipe = recipe; }

        public ValidationFinding Add(ValidationSeverity sev, string message, Object context = null, string contextPath = null)
        {
            var f = new ValidationFinding(sev, Recipe, message, context, contextPath);
            Findings.Add(f);
            return f;
        }
    }

    /// <summary>All recipe results for one scene row of the dashboard.</summary>
    public sealed class SceneHealthReport
    {
        public string SceneName;
        public string ScenePath;
        public SceneKind Kind;
        public readonly List<RecipeResult> Recipes = new List<RecipeResult>();

        public RecipeResult Get(string recipe) => Recipes.FirstOrDefault(r => r.Recipe == recipe);
    }

    /// <summary>
 /// The Scene Health Dashboard recipe engine (P14). Read-only; every finding
    /// names the offending object. Per-scene recipes run on a LOADED scene (the window opens Build
    /// Settings scenes additively, scans, closes); Enemy-prefab and Build-Settings recipes are
    /// project-wide. Severity policy: FAIL = would break at runtime / violates a law (R2/R9/R11),
    /// WARN = authoring gap the feature silently tolerates, INFO = density/context numbers.
    /// </summary>
    public static class SceneHealthRules
    {
        public const string MustHaves = "Must-haves";
        public const string FeatureWiring = "Wiring";
        public const string Counts = "Counts";
        public const string Timelines = "Timelines";
        public const string Volumes = "Volumes";
        public const string References = "References";
        public static readonly string[] SceneRecipes = { MustHaves, FeatureWiring, Counts, Timelines, Volumes, References };

        public const string EnemyPrefabs = "Enemy prefabs";
        public const string BuildSettings = "Build Settings";
        public const string WorldGraph = "World graph";
        public const string CodeLint = "Code lint";

        // ── per-scene entry point ─────────────────────────────────────────────────
        public static SceneHealthReport EvaluateScene(Scene scene)
        {
            var report = new SceneHealthReport
            {
                SceneName = scene.name,
                ScenePath = scene.path,
                Kind = SceneClassifier.Classify(scene)
            };
            var comps = SceneScan.ComponentsIn(scene).ToList();
            bool isArea = report.Kind == SceneKind.Area;
            bool isPersistent = report.Kind == SceneKind.Persistent;

            report.Recipes.Add(isArea ? CheckMustHaves(scene, comps) : NA(MustHaves));
            report.Recipes.Add(isArea ? CheckFeatureWiring(scene, comps) : NA(FeatureWiring));
            report.Recipes.Add(isArea ? CheckCounts(scene, comps) : NA(Counts));
            report.Recipes.Add(CheckTimelines(scene, comps));                       // any scene can host a director
            report.Recipes.Add(isArea || isPersistent ? CheckVolumes(scene, comps, isPersistent) : NA(Volumes));
            report.Recipes.Add(CheckReferences(scene, comps));                      // R2 + null-required, any scene
            return report;
        }

        private static RecipeResult NA(string name) => new RecipeResult(name) { NotApplicable = true };

        // ── Recipe: scene must-haves (area) ───────────────────────────────────────
        private static RecipeResult CheckMustHaves(Scene scene, List<Component> comps)
        {
            var r = new RecipeResult(MustHaves);

            // LocationEntrances — present, and one default (comesFrom == null) for unknown arrivals.
            var entrances = comps.OfType<LocationEntrance>().ToList();
            if (entrances.Count == 0)
                r.Add(ValidationSeverity.Error,
                    "No LocationEntrance — twins cannot be positioned on arrival (SoftReset/streaming).",
                    null, scene.name);
            else if (entrances.All(e => e.ComesFrom != null))
                r.Add(ValidationSeverity.Warning,
                    "No DEFAULT LocationEntrance (comesFrom = null) — arrivals from an unlisted direction have no fallback.",
                    entrances[0], scene.name);

            // NavMesh — a NavMeshSurface with data, or legacy baked NavMeshData beside the scene.
            bool hasSurface = comps.Any(c => c.GetType().Name == "NavMeshSurface");
            bool hasBaked = HasBakedNavMesh(scene) || comps.Any(c =>
                c.GetType().Name == "NavMeshSurface" && SurfaceHasData(c));
            if (!hasSurface && !HasBakedNavMesh(scene))
            {
                string scenePath = scene.path;
                r.Add(ValidationSeverity.Error, "No NavMesh (no NavMeshSurface, no baked NavMeshData beside the scene) — agents cannot navigate.", null, scene.name)
                    .WithFix("Add NavMeshSurface", () => AddNavMeshSurface(scenePath));
            }
            else if (hasSurface && !hasBaked)
                r.Add(ValidationSeverity.Warning, "NavMeshSurface present but not baked (navMeshData is null).",
                    comps.First(c => c.GetType().Name == "NavMeshSurface"), scene.name);

            // SpawnZones — points + config chain.
            foreach (var zone in comps.OfType<SpawnZone>())
            {
                bool hasPoints = (zone.leftSpawnPoints != null && zone.leftSpawnPoints.Length > 0)
                              || (zone.rightSpawnPoints != null && zone.rightSpawnPoints.Length > 0);
                if (!hasPoints)
                    r.Add(ValidationSeverity.Error, $"SpawnZone '{zone.name}' has no left/right spawn points — nothing can spawn.", zone, PathOf(zone));
                if (zone.areaConfig == null)
                    r.Add(ValidationSeverity.Error, $"SpawnZone '{zone.name}' has no AreaZoneConfig.", zone, PathOf(zone));
                else if (zone.areaConfig.spawnSetup == null)
                    r.Add(ValidationSeverity.Warning, $"SpawnZone '{zone.name}' config '{zone.areaConfig.name}' has no SpawnSetupConfig — no enemy roster.", zone.areaConfig, PathOf(zone));
            }

            // WorldLocationSO — exists + adjacency bidirectional for THIS scene.
            var location = LoadAll<WorldLocationSO>().FirstOrDefault(w => w != null && w.scene.Name == scene.name);
            if (location == null)
            {
                string sceneName = scene.name;
                r.Add(ValidationSeverity.Error, $"No WorldLocationSO references '{scene.name}' — the area cannot be streamed/reached.", null, scene.name)
                    .WithFix("Create WorldLocationSO", () => CreateWorldLocationFor(sceneName));
            }
            else if (location.adjacentLocations != null)
                foreach (var adj in location.adjacentLocations.Where(a => a != null))
                    if (adj.adjacentLocations == null || !adj.adjacentLocations.Contains(location))
                        r.Add(ValidationSeverity.Warning,
                            $"Adjacency not bidirectional: '{location.name}' → '{adj.name}' but not back — occupancy streaming will desync.",
                            location, AssetDatabase.GetAssetPath(location));

            // QTE anchor where QTE content exists.
            bool hasQte = comps.Any(c => c is QTEZoneTrigger || c is QTETriggerPoint);
            if (hasQte && !comps.Any(c => c is QTESceneAnchor))
                r.Add(ValidationSeverity.Error, "QTE triggers/points exist but no QTESceneAnchor — the QTE can never start.", null, scene.name);

            // R9 — world-space canvases need a camera resolver.
            foreach (var canvas in comps.OfType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.WorldSpace || canvas.worldCamera != null) continue;
                bool hasResolver = canvas.GetComponents<Component>().Any(x => x != null && x.GetType().Name == "WorldSpaceCanvasCamera");
                if (!hasResolver)
                    r.Add(ValidationSeverity.Warning,
                        $"World-space Canvas '{canvas.name}' has no worldCamera and no WorldSpaceCanvasCamera resolver (R9).",
                        canvas, PathOf(canvas));
            }

            // SceneLoadTriggers — target set, trigger collider, layer mask non-empty.
            foreach (var trig in comps.OfType<SceneLoadTrigger>())
            {
                var so = new SerializedObject(trig);
                if (so.FindProperty("targetLocation").objectReferenceValue == null)
                    r.Add(ValidationSeverity.Error, $"SceneLoadTrigger '{trig.name}' has no targetLocation — crossing it streams nothing.", trig, PathOf(trig));
                var col = trig.GetComponent<Collider>();
                if (col == null || !col.isTrigger)
                    r.Add(ValidationSeverity.Warning, $"SceneLoadTrigger '{trig.name}' collider missing or not IsTrigger.", trig, PathOf(trig));
                var layerProp = so.FindProperty("playerLayer");
                if (layerProp != null && layerProp.intValue == 0)
                    r.Add(ValidationSeverity.Warning, $"SceneLoadTrigger '{trig.name}' playerLayer is Nothing — it will never fire.", trig, PathOf(trig));
            }

            // AreaSpawnPoints — fine unless twins start/are positioned here (info-only, ported from Validate).
            if (!comps.Any(c => c is AreaSpawnPoints))
            {
                string scenePath = scene.path;
                r.Add(ValidationSeverity.Info, "No AreaSpawnPoints — fine unless twins start/are positioned here.", null, scene.name)
                    .WithFix("Add AreaSpawnPoints", () => AddAreaSpawnPoints(scenePath));
            }

            r.Summary = $"{entrances.Count} entrance(s), {comps.OfType<SpawnZone>().Count()} zone(s), {comps.OfType<SceneLoadTrigger>().Count()} trigger(s)";
            return r;
        }

        // ── Recipe: feature wiring — "N placed, M wired" ──────────────────────────
        private static RecipeResult CheckFeatureWiring(Scene scene, List<Component> comps)
        {
            var r = new RecipeResult(FeatureWiring);
            var parts = new List<string>();

            // QTE chain: per anchor — definition + ≥2 trigger points + zone trigger + root panel.
            var anchors = comps.OfType<QTESceneAnchor>().ToList();
            if (anchors.Count > 0)
            {
                int wired = 0;
                foreach (var a in anchors)
                {
                    var missing = new List<string>();
                    if (a.Definition == null) missing.Add("definition");
                    if (a.TriggerPoints == null || a.TriggerPoints.Length < 2) missing.Add("2 trigger points");
                    if (a.ZoneTrigger == null) missing.Add("zoneTrigger");
                    if (a.RootPanel == null) missing.Add("rootPanel UI");
                    if (missing.Count == 0) wired++;
                    else
                        r.Add(ValidationSeverity.Warning,
                            $"QTESceneAnchor '{a.name}' missing: {string.Join(", ", missing)}.", a, PathOf(a));
                }
                parts.Add($"QTE {wired}/{anchors.Count}");
            }

            // Ritual sites: placed POIs vs cue-book slot assigned (the P11 ritual glow).
            CountCueBookSlots<RitualSitePOI>(comps, r, parts, "Ritual");

            // Spawn-point POIs: same cue-book slot recipe.
            CountCueBookSlots<SpawnPointPOI>(comps, r, parts, "SpawnPOI");

            // Checkpoints: trigger collider present.
            var checkpoints = comps.Where(c => c.GetType().Name == "CheckPointTrigger").ToList();
            if (checkpoints.Count > 0)
            {
                int wired = 0;
                foreach (var cp in checkpoints)
                {
                    var col = cp.GetComponent<Collider>();
                    if (col != null && col.isTrigger) wired++;
                    else r.Add(ValidationSeverity.Warning, $"CheckPointTrigger '{cp.name}' has no trigger collider.", cp, PathOf(cp));
                }
                parts.Add($"Checkpoint {wired}/{checkpoints.Count}");
            }

            // Tutorial presence (steps resolve at runtime via TutorialStepContext — here only presence).
            int tutorial = comps.Count(c => c.GetType().Name == "TutorialDirector");
            if (tutorial > 0) parts.Add($"Tutorial ×{tutorial}");

            // POI energy emitters (ecology feed) — every POIBase should carry a PoiEnergyEmitter.
            var allPois = comps.OfType<POIBase>().ToList();
            if (allPois.Count > 0)
            {
                int wired = allPois.Count(p => p.GetComponent<PoiEnergyEmitter>() != null);
                foreach (var poi in allPois.Where(p => p.GetComponent<PoiEnergyEmitter>() == null))
                    r.Add(ValidationSeverity.Warning,
                        $"{poi.GetType().Name} '{poi.name}' has no PoiEnergyEmitter — it feeds no ambient energy (POI ecology).",
                        poi, PathOf(poi))
                        .WithFix("Wire POI ecology", PoiEcologyAuthoring.Wire);
                parts.Add($"Emitter {wired}/{allPois.Count}");
            }

            r.Summary = parts.Count > 0 ? string.Join(" · ", parts) : "no wired features";
            return r;
        }

        private static void CountCueBookSlots<T>(List<Component> comps, RecipeResult r, List<string> parts, string label)
            where T : Component
        {
            var pois = comps.OfType<T>().ToList();
            if (pois.Count == 0) return;
            int wired = 0;
            foreach (var poi in pois)
            {
                var so = new SerializedObject(poi);
                var prop = so.FindProperty("_cueBook");
                if (prop != null && prop.objectReferenceValue != null) wired++;
                else
                    r.Add(ValidationSeverity.Warning,
                        $"{typeof(T).Name} '{poi.name}' has no _cueBook — its cue plays nothing (null-safe but dark).",
                        poi, PathOf(poi));
            }
            parts.Add($"{label} {wired}/{pois.Count}");
        }

        // ── Recipe: content counts (info-only density view) ──────────────────────
        private static RecipeResult CheckCounts(Scene scene, List<Component> comps)
        {
            var r = new RecipeResult(Counts);
            int zones = comps.OfType<SpawnZone>().Count();
            int pois = comps.Count(c => c is SpawnPointPOI || c is RitualSitePOI || c.GetType().Name == "BarrierPOI");
            int traps = comps.Count(c => c.GetType().Name.Contains("Trap") && c is MonoBehaviour && !(c.GetType().Name.Contains("Trigger")));
            int orbs = comps.Count(c => c.GetType().Name == "SkillPointOrb");
            int checkpoints = comps.Count(c => c.GetType().Name == "CheckPointTrigger");
            r.Summary = $"{zones} zones · {pois} POIs · {traps} traps · {orbs} orbs · {checkpoints} checkpoints";
            r.Add(ValidationSeverity.Info, $"Density: {r.Summary}", null, scene.name);
            return r;
        }

        // ── Recipe: timeline audit (R11 / BUG-032 class) ──────────────────────────
        private static RecipeResult CheckTimelines(Scene scene, List<Component> comps)
        {
            var r = new RecipeResult(Timelines);
            var directors = comps.OfType<PlayableDirector>().ToList();
            if (directors.Count == 0) { r.NotApplicable = true; return r; }

            int totalNull = 0;
            foreach (var director in directors)
            {
                if (!(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track.muted) continue;
                    var binding = director.GetGenericBinding(track);

                    // Null binding: legal ONLY for tracks TimelineBindingResolver rebinds at runtime
                    // (Cinemachine/Animation → Persistent targets, R11 mech 1) and for markers/signals.
                    bool needsBinding = track.outputs.Any(o => o.outputTargetType != null);
                    bool runtimeRebound = track.GetType().Name.Contains("Cinemachine") || track is AnimationTrack;
                    if (needsBinding && binding == null && !runtimeRebound)
                    {
                        totalNull++;
                        r.Add(ValidationSeverity.Warning,
                            $"'{director.name}' track '{track.name}' ({track.GetType().Name}) has a NULL binding (BUG-032 class — authored pre-multiscene?).",
                            director, PathOf(director));
                    }

                    // R11: Activation Tracks must never control ancestors of gameplay-logic objects.
                    if (track is ActivationTrack && binding is GameObject go && go != null)
                    {
                        var logic = go.GetComponentsInChildren<Component>(true).FirstOrDefault(c =>
                            c is SceneLoadTrigger || c is SpawnZone || c is QTEZoneTrigger ||
                            c is LocationEntrance || (c != null && c.GetType().Name == "CheckPointTrigger"));
                        if (logic != null)
                            r.Add(ValidationSeverity.Error,
                                $"'{director.name}' Activation Track '{track.name}' controls '{go.name}', an ancestor of gameplay logic " +
                                $"({logic.GetType().Name} '{logic.name}') — R11: SetActive(true) no-ops under an inactive ancestor (the rescue-checkpoint bug).",
                                director, PathOf(director));
                    }
                }
            }
            r.Summary = $"{directors.Count} director(s), {totalNull} null binding(s)";
            return r;
        }

 // ── Recipe: volume architecture ─
        private static RecipeResult CheckVolumes(Scene scene, List<Component> comps, bool isPersistent)
        {
            var r = new RecipeResult(Volumes);
            var volumes = comps.OfType<Volume>().ToList();
            var globals = volumes.Where(v => v.isGlobal).ToList();

            if (isPersistent)
            {
                // Exactly one global story-grade volume at priority 0 (StoryGradeDirector drives it — P17).
                var storyGrade = globals.Where(v => Mathf.Approximately(v.priority, 0f)).ToList();
                if (storyGrade.Count == 0)
                    r.Add(ValidationSeverity.Warning,
                        "No global Volume at priority 0 in Persistent — the story-grade layer (P17 StoryGradeDirector) has no volume to drive.",
                        null, scene.name);
                else if (storyGrade.Count > 1)
                    r.Add(ValidationSeverity.Error,
                        $"{storyGrade.Count} global Volumes at priority 0 in Persistent — exactly ONE story-grade volume is allowed.",
                        storyGrade[1], PathOf(storyGrade[1]));

                // FailureResetSequencer must have its post-process volume wired.
                var sequencer = comps.FirstOrDefault(c => c is FailureResetSequencer);
                if (sequencer != null)
                {
                    var so = new SerializedObject(sequencer);
                    var prop = so.FindProperty("_postProcessVolume");
                    if (prop != null && prop.objectReferenceValue == null)
                        r.Add(ValidationSeverity.Warning,
                            "FailureResetSequencer._postProcessVolume is unwired — the failure sting (desat/vignette/CA) plays nothing.",
                            sequencer, PathOf(sequencer));
                }
                // Grade profile completeness — the P17 StoryGradeDirector rows + failure sting.
                string[] gradeNames = { "Grade_Act1_Warm", "Grade_Shock", "Grade_EarlyFear", "Grade_MidPurpose",
                                        "Grade_LateChaos", "Grade_Ending_Losing", "FailureReset_Sting" };
                var missingProfiles = gradeNames
                    .Where(n => AssetDatabase.LoadAssetAtPath<VolumeProfile>($"Assets/Settings/Grading/{n}.asset") == null)
                    .ToList();
                if (missingProfiles.Count > 0)
                    r.Add(ValidationSeverity.Warning,
                        $"Missing grade profile(s): {string.Join(", ", missingProfiles)} — StoryGradeDirector rows will have nothing to play.",
                        null, "Assets/Settings/Grading")
                        .WithFix("Create grade profiles", GradeProfileAuthoring.CreateAll);

                r.Summary = $"{globals.Count} global volume(s)";
                return r;
            }

            // Area scene: exactly one global IDENTITY volume (priority 10, profile assigned); no stray globals.
            var identity = globals.Where(v => Mathf.Approximately(v.priority, 10f)).ToList();
            var strays = globals.Where(v => !Mathf.Approximately(v.priority, 10f)).ToList();

            if (identity.Count == 0)
                r.Add(ValidationSeverity.Warning,
 "No area identity volume (global, priority 10) — this area has no colour identity.",
                    null, scene.name);
            else
            {
                if (identity.Count > 1)
                    r.Add(ValidationSeverity.Error,
                        $"{identity.Count} identity volumes (global prio 10) — exactly one per area.", identity[1], PathOf(identity[1]));
                foreach (var v in identity.Where(v => v.sharedProfile == null))
                    r.Add(ValidationSeverity.Warning, $"Identity volume '{v.name}' has no profile assigned.", v, PathOf(v));
                foreach (var v in identity.Where(v => v.sharedProfile != null))
                    r.Add(ValidationSeverity.Info, $"Area identity profile: '{v.sharedProfile.name}' (volume '{v.name}', weight {v.weight:0.##}).", v, PathOf(v));
            }

            foreach (var stray in strays)
                r.Add(ValidationSeverity.Error,
                    $"Stray GLOBAL volume '{stray.name}' (priority {stray.priority}) in an area scene — area globals other than " +
 "the priority-10 identity volume fight the Persistent story grade. Make it local or move it.",
                    stray, PathOf(stray));

            // Local prio-20 volumes (crack desat class) should carry a profile.
            foreach (var v in volumes.Where(v => !v.isGlobal && Mathf.Approximately(v.priority, 20f) && v.sharedProfile == null))
                r.Add(ValidationSeverity.Warning, $"Local priority-20 volume '{v.name}' (crack-desat class) has no profile.", v, PathOf(v));

            r.Summary = $"{identity.Count} identity, {strays.Count} stray global(s)";
            return r;
        }

        // ── Project recipe: enemy prefab health ──────────────────────────────────
        public static RecipeResult CheckEnemyPrefabs()
        {
            var r = new RecipeResult(EnemyPrefabs);
            int total = 0, healthy = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Models/Prefabs/Enemies" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<Enemy>() == null) continue;
                total++;
                bool ok = true;

                // Missing scripts = deleted components still serialized (the post-P11 EnemyVFXController class).
                int missing = prefab.GetComponentsInChildren<Transform>(true)
                    .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing > 0)
                {
                    ok = false;
                    r.Add(ValidationSeverity.Error,
                        $"'{prefab.name}' has {missing} missing script(s) — a deleted component (EnemyVFXController class?) is still serialized. Remove via GameObject ▸ Remove Missing Scripts or reserialize.",
                        prefab, path);
                }

                if (prefab.GetComponentInChildren<ManpuSlot>(true) == null)
                {
                    ok = false;
                    r.Add(ValidationSeverity.Warning,
 $"'{prefab.name}' has no ManpuSlot — no glyphs, no mood aura, no ability tells.",
                        prefab, path);
                }

                if (prefab.GetComponentInChildren<MaterialRevealDriver>(true) == null)
                    r.Add(ValidationSeverity.Info,
                        $"'{prefab.name}' has no MaterialRevealDriver — spawn reveal-delay is inactive for it (opt-in, fine if unwanted).",
                        prefab, path);

                // POI ecology: brains with dark energy should idle-visit POIs (SeekEnergy pair).
                if (prefab.GetComponent<PoTGOAPBrainBase>() != null
                    && prefab.GetComponent<EnemyDarkEnergy>() != null
                    && prefab.GetComponent<SiphonGhost>() == null
                    && prefab.GetComponent<GOAPGoalSeekEnergy>() == null)
                    r.Add(ValidationSeverity.Warning,
                        $"'{prefab.name}' has a GOAP brain + EnemyDarkEnergy but no GOAPGoalSeekEnergy — it never idle-visits POIs (ecology).",
                        prefab, path)
                        .WithFix("Wire POI ecology", PoiEcologyAuthoring.Wire);

                if (ok) healthy++;
            }
            r.Summary = $"{healthy}/{total} healthy";
            return r;
        }

        // ── Project recipe: Build Settings order + temp-scene hygiene ────────────
        public static RecipeResult CheckBuildSettings()
        {
            var r = new RecipeResult(BuildSettings);
            var scenes = EditorBuildSettings.scenes;
            var enabled = scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToList();

            string[] expectedHead = { "Bootstrap", "Persistent", "Intro" };
            for (int i = 0; i < expectedHead.Length; i++)
                if (enabled.Count <= i || enabled[i] != expectedHead[i])
                    r.Add(ValidationSeverity.Error,
                        $"Build Settings slot {i} must be '{expectedHead[i]}' (got '{(enabled.Count > i ? enabled[i] : "<none>")}') — boot order is load-bearing.",
                        null, "Build Settings");

            string[] tempNames = { "Restore", "Trees", "TestLab", "SampleScene" };
            foreach (var s in scenes.Where(s => !string.IsNullOrEmpty(s.path)))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
                if (tempNames.Contains(name) && s.enabled)
                    r.Add(ValidationSeverity.Error,
                        $"Temp/dev scene '{name}' is ENABLED in Build Settings — it must never ship.", null, s.path);
            }

            r.Summary = $"{enabled.Count} enabled scene(s)";
            return r;
        }

        // ── Recipe: references (R2 cross-scene + [RequiredReference] nulls) ───────
        // Ported from the retired Validate window. Cross-scene detection only sees violations
        // whose TARGET scene is also loaded (an unloaded target serializes as null) — scan with
        // Persistent open for full R2 coverage.
        private static RecipeResult CheckReferences(Scene scene, List<Component> comps)
        {
            var r = new RecipeResult(References);
            int crossScene = 0, nullRequired = 0;

            foreach (var hit in SceneScan.CrossSceneRefs(scene))
            {
                crossScene++;
                r.Add(ValidationSeverity.Error,
                    $"{hit.Owner.GetType().Name}.{hit.PropertyPath} → '{hit.Target.name}' lives in another scene (R2). " +
                    "Cross-scene serialized refs won't serialize — resolve at runtime via Manager.Instance (R4) or a registry (R5).",
                    hit.Owner, PathOf(hit.Owner));
            }

            foreach (var c in comps)
            {
                foreach (var f in FieldsWithRequired(c.GetType()))
                {
                    var attr = f.GetCustomAttribute<RequiredReferenceAttribute>();
                    if (!IsUnityNull(f.GetValue(c))) continue;
                    nullRequired++;
                    string note = string.IsNullOrEmpty(attr.Note) ? "" : $" — {attr.Note}";
                    r.Add(ValidationSeverity.Error,
                        $"{c.GetType().Name}.{f.Name} is required but null{note}.", c, PathOf(c));
                }

                // curated well-known slot (ported from Validate's curated checks)
                if (c is AreaSpawnPoints asp && (asp.leftStart == null || asp.rightStart == null))
                    r.Add(ValidationSeverity.Warning,
                        "AreaSpawnPoints is missing leftStart/rightStart — twins can't be positioned on game start.",
                        asp, PathOf(asp));
            }

            r.Summary = $"{crossScene} cross-scene, {nullRequired} null required";
            return r;
        }

        // ── Project recipe: WorldLocationSO graph + AreaZoneConfig completeness ──
        public static RecipeResult CheckWorldGraph()
        {
            var r = new RecipeResult(WorldGraph);
            var all = LoadAll<WorldLocationSO>();

            var buildScenes = new HashSet<string>(EditorBuildSettings.scenes
                .Where(s => !string.IsNullOrEmpty(s.path))
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)));

            int startCount = 0;
            foreach (var w in all)
            {
                if (w == null) continue;
                if (w.isStartLocation) startCount++;

                if (!w.scene.IsValid)
                    r.Add(ValidationSeverity.Error, $"'{w.name}' has no scene assigned.",
                        w, AssetDatabase.GetAssetPath(w));
                else if (!buildScenes.Contains(w.scene.Name))
                    r.Add(ValidationSeverity.Error, $"'{w.name}' → scene '{w.scene.Name}' is not in Build Settings.",
                        w, AssetDatabase.GetAssetPath(w));

                if (w.adjacentLocations != null)
                    foreach (var adj in w.adjacentLocations)
                    {
                        if (adj == null) continue;
                        if (adj.adjacentLocations == null || !adj.adjacentLocations.Contains(w))
                            r.Add(ValidationSeverity.Warning,
                                $"Adjacency not symmetric: '{w.name}' → '{adj.name}', but '{adj.name}' doesn't list '{w.name}' back.",
                                w, AssetDatabase.GetAssetPath(w));
                    }
            }

            if (all.Count > 0 && startCount == 0)
                r.Add(ValidationSeverity.Error, "No WorldLocationSO is marked isStartLocation — the twins have no start area.");
            else if (startCount > 1)
                r.Add(ValidationSeverity.Error, $"{startCount} WorldLocationSOs are marked isStartLocation — exactly one is allowed.");

            foreach (var cfg in LoadAll<AreaZoneConfig>())
            {
                if (cfg == null) continue;
                var missing = new List<string>();
                if (cfg.spawnSetup == null) missing.Add("spawnSetup");
                if (cfg.environment == null) missing.Add("environment");
                if (cfg.groupSpawn == null) missing.Add("groupSpawn");
                if (missing.Count > 0)
                {
                    var target = cfg;
                    r.Add(ValidationSeverity.Warning,
                        $"AreaZoneConfig '{cfg.name}' is missing sub-SO(s): {string.Join(", ", missing)}.",
                        cfg, AssetDatabase.GetAssetPath(cfg))
                        .WithFix("Fill sub-SOs", () => FillSubSos(target));
                }
            }

            r.Summary = $"{all.Count} location(s), {startCount} start";
            return r;
        }

        // ── Project recipe: static code lint (Rulebook / Banned-Lazy-Work) ───────
        public static RecipeResult CheckCodeLint()
        {
            var r = new RecipeResult(CodeLint);
            CodeLintRules.Collect(r.Findings);
            int e = r.Findings.Count(f => f.Severity == ValidationSeverity.Error);
            int w = r.Findings.Count(f => f.Severity == ValidationSeverity.Warning);
            r.Summary = $"{e} error(s), {w} warning(s), {r.Findings.Count - e - w} candidate(s)";
            return r;
        }

        // ── fixes (user-triggered from the detail pane; scene fixes require the scene loaded) ──
        private static void CreateWorldLocationFor(string sceneName)
        {
            var so = ScriptableObject.CreateInstance<WorldLocationSO>();
            var sObj = new SerializedObject(so);
            var sceneProp = sObj.FindProperty("scene").FindPropertyRelative("_name");
            if (sceneProp != null) sceneProp.stringValue = sceneName;
            sObj.ApplyModifiedPropertiesWithoutUndo();

            string dir = "Assets/Scripts/SceneLaoder/Data";
            if (!AssetDatabase.IsValidFolder(dir)) dir = "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/Location_{sceneName}.asset");
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(so);
            Debug.Log($"[SceneHealth] Created {path}. Assign its scene in Build Settings + set adjacency/isStartLocation.", so);
        }

        private static bool TryGetLoadedScene(string scenePath, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded) return true;
            Debug.LogWarning($"[SceneHealth] '{scenePath}' is not open — open the scene, rescan, then run this fix.");
            return false;
        }

        private static void AddAreaSpawnPoints(string scenePath)
        {
            if (!TryGetLoadedScene(scenePath, out var scene)) return;
            var go = new GameObject("AreaSpawnPoints");
            Undo.RegisterCreatedObjectUndo(go, "Add AreaSpawnPoints");
            SceneManager.MoveGameObjectToScene(go, scene);
            var asp = go.AddComponent<AreaSpawnPoints>();
            var l = new GameObject("SpawnLeft").transform; l.SetParent(go.transform); l.localPosition = new Vector3(-2, 0, 0);
            var right = new GameObject("SpawnRight").transform; right.SetParent(go.transform); right.localPosition = new Vector3(2, 0, 0);
            asp.leftStart = l; asp.rightStart = right;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            EditorGUIUtility.PingObject(go);
        }

        private static void AddNavMeshSurface(string scenePath)
        {
            if (!TryGetLoadedScene(scenePath, out var scene)) return;
            var navType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (navType == null) { Debug.LogError("[SceneHealth] NavMeshSurface type not found — add an AI Navigation surface manually."); return; }
            var go = new GameObject("NavMeshSurface");
            Undo.RegisterCreatedObjectUndo(go, "Add NavMeshSurface");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent(navType);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            EditorGUIUtility.PingObject(go);
        }

        private static void FillSubSos(AreaZoneConfig cfg)
        {
            string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(cfg)).Replace('\\', '/');
            var so = new SerializedObject(cfg);
            if (cfg.spawnSetup == null)
                so.FindProperty("spawnSetup").objectReferenceValue = CreateSub<SpawnSetupConfig>(dir, "SpawnSetup", cfg.name);
            if (cfg.environment == null)
                so.FindProperty("environment").objectReferenceValue = CreateSub<ZoneEnvironmentConfig>(dir, "Environment", cfg.name);
            if (cfg.groupSpawn == null)
                so.FindProperty("groupSpawn").objectReferenceValue = CreateSub<GroupSpawnConfig>(dir, "GroupSpawn", cfg.name);
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(cfg);
        }

        private static T CreateSub<T>(string dir, string prefix, string area) where T : ScriptableObject
        {
            var sub = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(sub, AssetDatabase.GenerateUniqueAssetPath($"{dir}/{prefix}_{area}.asset"));
            return sub;
        }

        // ── reflection helpers (RequiredReference scan, ported from Validate) ─────
        private static IEnumerable<System.Reflection.FieldInfo> FieldsWithRequired(System.Type type)
        {
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    if (f.GetCustomAttribute<RequiredReferenceAttribute>() != null)
                        yield return f;
        }

        private static bool IsUnityNull(object v)
        {
            if (v == null) return true;
            if (v is Object uo) return uo == null;
            return false;
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private static bool SurfaceHasData(Component surface)
        {
            var so = new SerializedObject(surface);
            var prop = so.FindProperty("m_NavMeshData");
            return prop != null && prop.objectReferenceValue != null;
        }

        private static bool HasBakedNavMesh(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path)) return false;
            string dir = System.IO.Path.GetDirectoryName(scene.path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir)) return false;
            return AssetDatabase.FindAssets("t:NavMeshData", new[] { dir }).Length > 0;
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }

        private static string PathOf(Component c)
        {
            if (c == null) return "(null)";
            var t = c.transform;
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return $"{c.gameObject.scene.name}/{sb}";
        }
    }
}
