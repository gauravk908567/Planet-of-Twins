using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
 /// Tools ▸ Planet of Twins ▸ Scene Health Dashboard (P14).
    /// One row per Build-Settings scene, one coloured cell per recipe (green pass / yellow warn /
    /// red fail / grey n-a); click a cell to see its findings (Select pings the offender).
    /// Scanning opens each not-open scene ADDITIVELY, evaluates, and closes it without saving —
    /// the user's open scenes are never touched. Project rows (Enemy prefabs, Build Settings)
    /// need no scene loads.
    /// </summary>
    public class SceneHealthDashboardWindow : EditorWindow
    {
        private readonly List<SceneHealthReport> _reports = new List<SceneHealthReport>();
        private RecipeResult _enemyPrefabs;
        private RecipeResult _buildSettings;
        private RecipeResult _worldGraph;
        private RecipeResult _codeLint;

        private Vector2 _gridScroll, _detailScroll;
        private RecipeResult _selected;
        private string _selectedLabel = "";
        private string _selectedScope = "";      // scene path (or "PROJECT") of the selected cell — for waiver keys
        private bool _showWaived = true;          // keep not-required findings visible (as neutral lines)
        private bool _hasRun;
        private double _lastRunAt;

        private static readonly Color PassCol = new Color(0.35f, 0.75f, 0.35f);
        private static readonly Color WarnCol = new Color(0.95f, 0.75f, 0.20f);
        private static readonly Color FailCol = new Color(0.90f, 0.30f, 0.25f);
        private static readonly Color NaCol   = new Color(0.45f, 0.45f, 0.45f);

        [MenuItem("Tools/Planet of Twins/Scene Health Dashboard")]
        public static void Open()
        {
            var w = GetWindow<SceneHealthDashboardWindow>("PoT Scene Health");
            w.minSize = new Vector2(900, 420);
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (!_hasRun)
            {
                EditorGUILayout.HelpBox(
                    "Scan All: opens every enabled Build Settings scene additively (plus any open scenes), " +
                    "runs the recipes, then closes what it opened — nothing is saved or modified. " +
                    "Scan Open: only the scenes already open.", MessageType.Info);
                return;
            }
            DrawGrid();
            DrawDetail();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Scan All (build scenes)", EditorStyles.toolbarButton, GUILayout.Width(150))) ScanAll();
                if (GUILayout.Button("Scan Open", EditorStyles.toolbarButton, GUILayout.Width(80))) ScanOpen();
                GUILayout.Space(12);
                _showWaived = GUILayout.Toggle(_showWaived, "Show not-required", EditorStyles.toolbarButton, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (_hasRun) GUILayout.Label($"scanned {System.DateTime.Now.AddSeconds(_lastRunAt - EditorApplication.timeSinceStartup):HH:mm:ss}", EditorStyles.miniLabel);
            }
        }

        // ── scanning ─────────────────────────────────────────────────────────────
        private void ScanOpen()
        {
            _reports.Clear();
            foreach (var s in SceneScan.LoadedScenes())
                _reports.Add(SceneHealthRules.EvaluateScene(s));
            RunProjectRecipes();
        }

        private void ScanAll()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Scene Health", "Exit play mode first — Scan All opens scenes in the editor.", "OK");
                return;
            }

            _reports.Clear();
            var openPaths = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++) openPaths.Add(SceneManager.GetSceneAt(i).path);

            var paths = EditorBuildSettings.scenes
                .Where(b => b.enabled && !string.IsNullOrEmpty(b.path))
                .Select(b => b.path)
                .Concat(openPaths)           // open-but-unlisted scenes (e.g. TestLab) get a row too
                .Distinct().ToList();

            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar("Scene Health", path, (float)i / paths.Count);

                    bool alreadyOpen = openPaths.Contains(path);
                    Scene sc = alreadyOpen ? SceneManager.GetSceneByPath(path)
                                           : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (sc.IsValid() && sc.isLoaded)
                        _reports.Add(SceneHealthRules.EvaluateScene(sc));
                    if (!alreadyOpen && sc.IsValid())
                        EditorSceneManager.CloseScene(sc, true);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            RunProjectRecipes();
        }

        private void RunProjectRecipes()
        {
            _enemyPrefabs = SceneHealthRules.CheckEnemyPrefabs();
            _buildSettings = SceneHealthRules.CheckBuildSettings();
            _worldGraph = SceneHealthRules.CheckWorldGraph();
            _codeLint = SceneHealthRules.CheckCodeLint();
            ApplyWaivers();
            _selected = null;
            _hasRun = true;
            _lastRunAt = EditorApplication.timeSinceStartup;
            Repaint();
        }

        // Stamp each finding with its persisted waiver state (post-scan, so re-scans keep marks).
        private void ApplyWaivers()
        {
            foreach (var report in _reports)
                foreach (var recipe in report.Recipes)
                    ApplyWaiversTo(report.ScenePath, recipe);
            ApplyWaiversTo("PROJECT", _enemyPrefabs);
            ApplyWaiversTo("PROJECT", _buildSettings);
            ApplyWaiversTo("PROJECT", _worldGraph);
            ApplyWaiversTo("PROJECT", _codeLint);
        }

        private static void ApplyWaiversTo(string scope, RecipeResult recipe)
        {
            if (recipe == null) return;
            foreach (var f in recipe.Findings)
            {
                string key = SceneHealthWaivers.Key(scope, f.Category, f.Message);
                f.Waived = SceneHealthWaivers.IsWaived(key);
                f.WaiverReason = f.Waived ? SceneHealthWaivers.GetReason(key) : null;
            }
        }

        // ── grid ─────────────────────────────────────────────────────────────────
        private void DrawGrid()
        {
            const float nameW = 150f, cellW = 118f, cellH = 22f;

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.MaxHeight(position.height * 0.55f));

            // header
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Scene", EditorStyles.boldLabel, GUILayout.Width(nameW));
                foreach (var recipe in SceneHealthRules.SceneRecipes)
                    GUILayout.Label(recipe, EditorStyles.boldLabel, GUILayout.Width(cellW));
            }

            foreach (var report in _reports)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{report.SceneName}  ({report.Kind})", GUILayout.Width(nameW), GUILayout.Height(cellH));
                    foreach (var recipe in SceneHealthRules.SceneRecipes)
                        DrawCell(report.Get(recipe), report.ScenePath, $"{report.SceneName} · {recipe}", cellW, cellH);
                }
            }

            // project rows
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("PROJECT", EditorStyles.boldLabel, GUILayout.Width(nameW), GUILayout.Height(cellH));
                DrawCell(_enemyPrefabs, "PROJECT", "Project · Enemy prefabs", cellW, cellH);
                DrawCell(_buildSettings, "PROJECT", "Project · Build Settings", cellW, cellH);
                DrawCell(_worldGraph, "PROJECT", "Project · World graph", cellW, cellH);
                DrawCell(_codeLint, "PROJECT", "Project · Code lint", cellW, cellH);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCell(RecipeResult result, string scope, string label, float w, float h)
        {
            if (result == null) { GUILayout.Label("—", GUILayout.Width(w), GUILayout.Height(h)); return; }

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = ColorOf(result.Status);
            // Badge counts ACTIVE (non-waived) findings only, so a fully-waived cell reads ✔.
            int eCount = result.Findings.Count(f => !f.Waived && f.Severity == ValidationSeverity.Error);
            int wCount = result.Findings.Count(f => !f.Waived && f.Severity == ValidationSeverity.Warning);
            string text = result.Status == RecipeStatus.NotApplicable ? "n/a"
                : result.Status == RecipeStatus.Pass ? "✔"
                : $"{eCount}E {wCount}W";
            var content = new GUIContent($" {result.Recipe}: {text}", result.Summary);
            if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(w), GUILayout.Height(h)))
            {
                _selected = result;
                _selectedScope = scope;
                _selectedLabel = label;
            }
            GUI.backgroundColor = prev;
        }

        private static Color ColorOf(RecipeStatus s)
        {
            switch (s)
            {
                case RecipeStatus.Pass: return PassCol;
                case RecipeStatus.Warn: return WarnCol;
                case RecipeStatus.Fail: return FailCol;
                default: return NaCol;
            }
        }

        // ── detail pane ───────────────────────────────────────────────────────────
        private void DrawDetail()
        {
            EditorGUILayout.Space(4);
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Click a cell to see its findings.", MessageType.None);
                return;
            }

            int waivedCount = _selected.Findings.Count(f => f.Waived);
            GUILayout.Label($"{_selectedLabel} — {_selected.Summary}" +
                            (waivedCount > 0 ? $"   ({waivedCount} not-required)" : ""), EditorStyles.boldLabel);
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            if (_selected.Findings.Count == 0)
                EditorGUILayout.HelpBox("Green — nothing to report.", MessageType.Info);

            // ToList — the waive / un-waive buttons mutate finding state mid-draw.
            foreach (var f in _selected.Findings.ToList())
            {
                if (f.Waived && !_showWaived) continue;
                string key = SceneHealthWaivers.Key(_selectedScope, f.Category, f.Message);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (f.Waived)
                    {
                        // Neutral info line — still visible, demoted out of the Pass/Warn/Fail status.
                        GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon.sml"), GUILayout.Width(20), GUILayout.Height(20));
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("✓ Marked not-required", EditorStyles.boldLabel);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label("reason:", GUILayout.Width(52));
                                string newReason = EditorGUILayout.TextField(f.WaiverReason ?? "");
                                if (newReason != (f.WaiverReason ?? ""))
                                {
                                    f.WaiverReason = newReason;
                                    SceneHealthWaivers.SetWaived(key, newReason);   // saved live
                                }
                            }
                            EditorGUILayout.LabelField(f.Message, EditorStyles.wordWrappedMiniLabel);
                            if (!string.IsNullOrEmpty(f.ContextPath))
                                EditorGUILayout.LabelField(f.ContextPath, EditorStyles.miniLabel);
                        }
                        if (GUILayout.Button("Un-waive", GUILayout.Width(70)))
                        {
                            SceneHealthWaivers.Remove(key);
                            f.Waived = false; f.WaiverReason = null;
                            GUIUtility.ExitGUI();
                        }
                    }
                    else
                    {
                        GUILayout.Label(Icon(f.Severity), GUILayout.Width(20), GUILayout.Height(20));
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField(f.Message, EditorStyles.wordWrappedLabel);
                            if (!string.IsNullOrEmpty(f.ContextPath))
                                EditorGUILayout.LabelField(f.ContextPath, EditorStyles.miniLabel);
                        }
                        if (f.Context != null && GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            Selection.activeObject = f.Context;
                            EditorGUIUtility.PingObject(f.Context);
                        }
                        if (f.Fix != null && GUILayout.Button(f.FixLabel ?? "Fix", GUILayout.Width(60)))
                        {
                            f.Fix();
                            _selected.Findings.Remove(f);   // cell status recomputes; rescan for full truth
                            GUIUtility.ExitGUI();           // list mutated mid-draw
                        }
                        if (GUILayout.Button("Not required", GUILayout.Width(90)))
                        {
                            SceneHealthWaivers.SetWaived(key, "");   // reason typed on the waived line
                            f.Waived = true; f.WaiverReason = "";
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static GUIContent Icon(ValidationSeverity s)
        {
            switch (s)
            {
                case ValidationSeverity.Error: return EditorGUIUtility.IconContent("console.erroricon.sml");
                case ValidationSeverity.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml");
                default: return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }
    }
}
