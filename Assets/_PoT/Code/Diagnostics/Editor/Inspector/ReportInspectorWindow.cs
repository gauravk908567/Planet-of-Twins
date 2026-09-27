using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PoT.Diagnostics.Editor
{
    /// <summary>
    /// The Report Inspector (game.md §27.6): open bug-report zips and read them.
    ///   • Summary: the player's words, version, hardware, settings, the screenshot, and "Load this save".
    ///   • Timeline: the breadcrumb trail, or a session log's crumbs with its warnings/errors in between.
    ///   • Problems: every warning/error grouped by signature; stack frames open the script at the line.
    ///   • Logs: a whole session log, filtered by level and text.
    ///   • Files: everything in the zip.
    ///   • With several reports loaded, "All reports" groups problems across them and shows what the affected
    ///     reports share (version, GPU, OS…).
    /// Drop zips or folders on the window, or use the toolbar. Reads only the documented report format, so it works
    /// for any game that uses the diagnostics package; "Load this save" comes from the game's
    /// <see cref="IReportSaveHandler"/>.
    /// </summary>
    public sealed class ReportInspectorWindow : EditorWindow
    {
        private enum Tab { Summary, Timeline, Problems, Logs, Files }

        private const string PrefFolder = "PoT.Diagnostics.ReportInspector.Folder";
        private const float ListWidth = 260f, RowHeight = 18f, LabelWidth = 150f;

        // What the across-reports view compares (section, key).
        private static readonly (string Section, string Key)[] CompareFields =
        {
            ("app", "Version"), ("app", "Commit"), ("app", "Build type"),
            ("system", "OS"), ("system", "GPU"), ("system", "Graphics API"), ("system", "CPU"), ("system", "RAM"),
        };
        private static readonly HashSet<string> OpenSections = new HashSet<string> { "app", "system", "crash" };

        [SerializeField] private List<string> _paths = new List<string>();
        [SerializeField] private int _selected = -1;
        [SerializeField] private Tab _tab;

        [NonSerialized] private List<InspectedReport> _reports;
        [NonSerialized] private List<ProblemGroup> _crossGroups = new List<ProblemGroup>();
        [NonSerialized] private List<IReportSaveHandler> _saveHandlers;
        [NonSerialized] private Texture2D _screenshot;
        [NonSerialized] private InspectedReport _screenshotOf;

        private Vector2 _listScroll, _pageScroll, _rowsScroll, _detailScroll;
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

        private int _timelineSource;
        private bool _timelineProblems = true;
        private string _timelineFilter = string.Empty;
        private int _selectedGroup = -1, _selectedCross = -1;
        private bool _showProblemsText;
        private int _logIndex;
        private string _logFilter = string.Empty;
        private bool _showInfo = true, _showCrumbs = true, _showNotes = true, _showWarnings = true, _showErrors = true;

        // Filtered rows, rebuilt only when their inputs change.
        private readonly List<LogEntry> _rows = new List<LogEntry>();
        private readonly List<InspectedReport.Crumb> _crumbRows = new List<InspectedReport.Crumb>();
        private string _rowsKey;
        private LogEntry _selectedEntry;
        private string _selectedEntryLog;
        private int _selectedCrumb = -1;
        [NonSerialized] private (InspectedReport Report, string Signature) _jumpTo;

        private GUIStyle _title, _wrapped, _key, _detail, _rightMini, _errorMini;

        [MenuItem("Window/Analysis/Report Inspector")]
        public static void Open() => GetWindow<ReportInspectorWindow>("Report Inspector").Show();

        /// <summary>Opens the window and loads <paramref name="paths"/> (zips or folders of zips).</summary>
        public static void Open(IEnumerable<string> paths)
        {
            var window = GetWindow<ReportInspectorWindow>("Report Inspector");
            window.AddPaths(paths);
            window.Show();
        }

        private void OnEnable()
        {
            _saveHandlers = FindSaveHandlers();
            _reports = new List<InspectedReport>();
            foreach (var path in _paths.ToArray())
                if (File.Exists(path)) _reports.Add(InspectedReport.Load(path));
            _paths = _reports.Select(r => r.SourcePath).ToList();
            if (_selected >= _reports.Count) _selected = -1;
            RebuildCross();
        }

        private void OnDisable()
        {
            if (_screenshot != null) DestroyImmediate(_screenshot);
        }

        // ── Loading ─────────────────────────────────────────────────────────────

        private void AddPaths(IEnumerable<string> paths)
        {
            if (_reports == null) OnEnable();
            var zips = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path)) zips.AddRange(Directory.GetFiles(path, "*.zip"));
                else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) zips.Add(path);
            }

            InspectedReport added = null;
            int count = 0;
            foreach (var zip in zips)
            {
                string full = Path.GetFullPath(zip);
                if (_reports.Any(r => string.Equals(Path.GetFullPath(r.SourcePath), full, StringComparison.OrdinalIgnoreCase)))
                    continue;
                added = InspectedReport.Load(full);
                _reports.Add(added);
                count++;
            }
            if (count == 0)
            {
                ShowNotification(new GUIContent(zips.Count == 0 ? "No report zips there" : "Already loaded"));
                return;
            }

            _reports.Sort((a, b) => string.CompareOrdinal(b.CreatedUtc, a.CreatedUtc));   // newest first
            _paths = _reports.Select(r => r.SourcePath).ToList();
            RebuildCross();
            Select(count == 1 ? _reports.IndexOf(added) : -1);
        }

        private void Clear()
        {
            _reports.Clear();
            _paths.Clear();
            _crossGroups.Clear();
            Select(-1);
        }

        private void RebuildCross()
        {
            var bySignature = new Dictionary<string, ProblemGroup>();
            _crossGroups = new List<ProblemGroup>();
            foreach (var report in _reports)
            {
                foreach (var group in report.Problems)
                {
                    if (!bySignature.TryGetValue(group.Signature, out var cross))
                    {
                        cross = new ProblemGroup(group.Signature, group.Sample, $"{report.Title} · {group.SampleLog}");
                        bySignature.Add(group.Signature, cross);
                        _crossGroups.Add(cross);
                    }
                    cross.Occurrences += group.Occurrences;
                    if (!cross.Reports.Contains(report)) cross.Reports.Add(report);
                }
            }
            ProblemGroup.Sort(_crossGroups);
            _selectedCross = -1;
        }

        private void Select(int index)
        {
            _selected = index;
            _selectedGroup = _selectedCrumb = -1;
            _selectedEntry = null;
            _logIndex = _timelineSource = 0;
            _rowsKey = null;
            _pageScroll = _rowsScroll = _detailScroll = Vector2.zero;
            Repaint();
        }

        private static List<IReportSaveHandler> FindSaveHandlers()
        {
            var handlers = new List<IReportSaveHandler>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IReportSaveHandler>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try { handlers.Add((IReportSaveHandler)Activator.CreateInstance(type)); }
                catch (Exception e) { Debug.LogWarning($"[Report Inspector] {type.Name} could not be created: {e.Message}"); }
            }
            return handlers;
        }

        // ── Frame ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_reports == null) OnEnable();
            if (_jumpTo.Report != null && Event.current.type == EventType.Layout)
            {
                var (report, signature) = _jumpTo;
                _jumpTo = default;
                Select(_reports.IndexOf(report));
                _tab = Tab.Problems;
                _selectedGroup = report.Problems.FindIndex(g => g.Signature == signature);
            }
            EnsureStyles();
            DrawToolbar();
            HandleDrop();

            if (_reports.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Drop bug-report zips (or a folder of them) on this window, or use Open Zip / Open Folder.\n\n" +
                    "Reports sent by players arrive as email attachments: save the zip, then drop it here. " +
                    "\"This PC's Reports\" opens the ones this game saved on this PC.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawReportList();
                using (new EditorGUILayout.VerticalScope())
                {
                    if (_selected < 0 || _selected >= _reports.Count) DrawAcross();
                    else DrawReport(_reports[_selected]);
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Each button replaces what the window shows (and the file panels are modal), so the frame ends there.
                if (GUILayout.Button("Open Zip…", EditorStyles.toolbarButton))
                {
                    string path = EditorUtility.OpenFilePanel("Open a bug report", LastFolder, "zip");
                    if (!string.IsNullOrEmpty(path)) { LastFolder = Path.GetDirectoryName(path); AddPaths(new[] { path }); }
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Open Folder…", EditorStyles.toolbarButton))
                {
                    string path = EditorUtility.OpenFolderPanel("Open a folder of bug reports", LastFolder, string.Empty);
                    if (!string.IsNullOrEmpty(path)) { LastFolder = path; AddPaths(new[] { path }); }
                    GUIUtility.ExitGUI();
                }
                string local = ReportCollector.ReportsFolder;
                if (GUILayout.Button(new GUIContent("This PC's Reports", "The reports this game saved on this PC:\n" + local),
                                     EditorStyles.toolbarButton))
                {
                    if (Directory.Exists(local)) AddPaths(new[] { local });
                    else ShowNotification(new GUIContent("No reports on this PC yet"));
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(_reports.Count == 1 ? "1 report" : $"{_reports.Count} reports", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(_reports.Count == 0))
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) { Clear(); GUIUtility.ExitGUI(); }
            }
        }

        private void HandleDrop()
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            var paths = DragAndDrop.paths;
            if (paths == null || !paths.Any(p => Directory.Exists(p) || p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddPaths(paths);
                e.Use();
                GUIUtility.ExitGUI();
            }
            e.Use();
        }

        private void DrawReportList()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Width(ListWidth)))
            {
                _listScroll = scroll.scrollPosition;
                if (_reports.Count > 1)
                    ListRow(-1, $"All {_reports.Count} reports",
                            _crossGroups.Count == 1 ? "1 different problem" : $"{_crossGroups.Count} different problems",
                            null, false);
                for (int i = 0; i < _reports.Count; i++)
                {
                    var report = _reports[i];
                    if (report.LoadError != null)
                    {
                        ListRow(i, Path.GetFileName(report.SourcePath), "Can't read: " + report.LoadError, null, true);
                        continue;
                    }
                    string kind = string.IsNullOrEmpty(report.Category) ? string.Empty : report.Category + " · ";
                    string badge = report.ErrorCount + report.WarningCount == 0
                        ? null : $"{report.ErrorCount} E  {report.WarningCount} W";
                    ListRow(i, $"{report.Title}   {report.CreatedLocal}", kind + report.DescriptionFirstLine, badge, false);
                }
            }
            var last = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(last.xMax, last.y, 1, last.height), SeparatorColor);
        }

        private void ListRow(int index, string title, string subtitle, string badge, bool failed)
        {
            Rect rect = GUILayoutUtility.GetRect(ListWidth - 20, 38, GUILayout.ExpandWidth(true));
            if (index == _selected) EditorGUI.DrawRect(rect, SelectedColor);
            GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width - 78, 18), title, EditorStyles.boldLabel);
            if (badge != null) GUI.Label(new Rect(rect.xMax - 72, rect.y + 3, 68, 16), badge, _rightMini);
            GUI.Label(new Rect(rect.x + 6, rect.y + 19, rect.width - 12, 16), subtitle,
                      failed ? _errorMini : EditorStyles.miniLabel);
            if (Clicked(rect)) { Select(index); GUIUtility.ExitGUI(); }
        }

        // ── One report ──────────────────────────────────────────────────────────

        private void DrawReport(InspectedReport report)
        {
            if (report.LoadError != null)
            {
                EditorGUILayout.HelpBox($"{report.SourcePath}\n\nThis file can't be read as a bug report: {report.LoadError}.",
                                        MessageType.Error);
                return;
            }

            var labels = new[]
            {
                "Summary", $"Timeline ({report.Breadcrumbs.Count})", $"Problems ({report.Problems.Count})",
                $"Logs ({report.SessionLogs.Count})", $"Files ({report.Files.Count})",
            };
            var tab = (Tab)GUILayout.Toolbar((int)_tab, labels, GUILayout.Height(22));
            if (tab != _tab) { _tab = tab; _rowsScroll = _detailScroll = _pageScroll = Vector2.zero; GUIUtility.ExitGUI(); }

            switch (_tab)
            {
                case Tab.Summary: DrawSummary(report); break;
                case Tab.Timeline: DrawTimeline(report); break;
                case Tab.Problems: DrawProblems(report); break;
                case Tab.Logs: DrawLogs(report); break;
                default: DrawFiles(report); break;
            }
        }

        private void DrawSummary(InspectedReport report)
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_pageScroll))
            {
                _pageScroll = scroll.scrollPosition;
                GUILayout.Label(report.Title, _title);
                Field("Made", report.CreatedLocal + "  (your local time)");
                Field("Type", string.IsNullOrEmpty(report.Category) ? "(none picked)" : report.Category);
                Field("Reply to", string.IsNullOrEmpty(report.Contact) ? "(no email left)" : report.Contact);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Field("File", report.SourcePath);
                    if (GUILayout.Button("Show", GUILayout.Width(50))) EditorUtility.RevealInFinder(report.SourcePath);
                }

                EditorGUILayout.Space(6);
                GUILayout.Label("What happened", EditorStyles.boldLabel);
                Wrapped(string.IsNullOrEmpty(report.Description) ? "(no text: the player only picked a type)" : report.Description);

                EditorGUILayout.Space(6);
                Field("Problems", report.Problems.Count == 0
                    ? "no warnings or errors in its session logs"
                    : $"{report.ErrorCount} error(s) and {report.WarningCount} warning(s): {report.Problems.Count} different message(s)");
                foreach (var log in report.SessionLogs)
                    Field(log.Name, log.End ?? "no end line: this run crashed, was killed, or was still running");

                DrawScreenshot(report);
                DrawSaves(report);

                EditorGUILayout.Space(6);
                foreach (var section in report.Sections)
                {
                    if (!Foldout(section.Name, OpenSections.Contains(section.Name))) continue;
                    EditorGUI.indentLevel++;
                    foreach (var (key, value) in section.Fields) Field(key, value);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawScreenshot(InspectedReport report)
        {
            if (report.Screenshot == null) return;
            if (_screenshotOf != report || _screenshot == null)
            {
                if (_screenshot != null) DestroyImmediate(_screenshot);
                _screenshot = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                _screenshot.LoadImage(report.Screenshot);
                _screenshotOf = report;
            }
            EditorGUILayout.Space(6);
            GUILayout.Label("Screenshot (the game right before the menu opened)", EditorStyles.boldLabel);
            float width = Mathf.Min(480f, position.width - ListWidth - 40f);
            float height = width * _screenshot.height / Mathf.Max(1, _screenshot.width);
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            GUI.DrawTexture(rect, _screenshot, ScaleMode.ScaleToFit);
            if (GUILayout.Button("Open Full Size", GUILayout.Width(120))) OpenExtracted(report, "screenshot.jpg");
        }

        private void DrawSaves(InspectedReport report)
        {
            var saves = report.Files.Where(f => f.Path.StartsWith(InspectedReport.SavePrefix, StringComparison.Ordinal)
                                                && f.WrittenBytes > 0).ToList();
            if (saves.Count == 0) return;
            EditorGUILayout.Space(6);
            GUILayout.Label("Save files", EditorStyles.boldLabel);
            bool anyHandler = false;
            foreach (var save in saves)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(save.Path, GUILayout.Width(LabelWidth + 60));
                    foreach (var handler in _saveHandlers)
                    {
                        if (!handler.CanLoad(save.Path)) continue;
                        anyHandler = true;
                        if (GUILayout.Button(handler.ButtonLabel(save.Path), GUILayout.Width(160)))
                        {
                            LoadSave(report, save.Path, handler);
                            GUIUtility.ExitGUI();   // after modal dialogs (and maybe Play mode), redraw from scratch
                        }
                    }
                    if (GUILayout.Button("Open", GUILayout.Width(60))) OpenExtracted(report, save.Path);
                    GUILayout.FlexibleSpace();
                }
            }
            if (!anyHandler)
                EditorGUILayout.HelpBox("No game code knows these files. A game adds \"Load this save\" with an " +
                                        "IReportSaveHandler in its editor code.", MessageType.None);
        }

        private void LoadSave(InspectedReport report, string zipPath, IReportSaveHandler handler)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Load this save", "Stop Play mode first: the game may be using its save files.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Load this save", handler.Explain(zipPath), "Load It", "Cancel")) return;

            string result;
            try { result = handler.Load(zipPath, report.ReadEntry(zipPath)); }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Load this save", "It couldn't be loaded: " + e.Message, "OK");
                return;
            }
            if (EditorUtility.DisplayDialog("Save loaded", result + "\n\nStart the game now?", "Play", "Not Now"))
                handler.Play();
        }

        private void DrawTimeline(InspectedReport report)
        {
            var sources = new List<string> { $"The run that made the report (report.json, {report.Breadcrumbs.Count} crumbs)" };
            sources.AddRange(report.SessionLogs.Select(l => l.Name + (l.End == null ? "   (no end: crashed or still running)" : string.Empty)));
            _timelineSource = Mathf.Clamp(_timelineSource, 0, sources.Count - 1);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int source = EditorGUILayout.Popup(_timelineSource, sources.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(380));
                if (source != _timelineSource) { _timelineSource = source; _selectedEntry = null; _selectedCrumb = -1; GUIUtility.ExitGUI(); }
                EditorGUI.BeginChangeCheck();
                if (_timelineSource > 0)
                    _timelineProblems = GUILayout.Toggle(_timelineProblems, "With warnings + errors", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                _timelineFilter = EditorGUILayout.TextField(_timelineFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck()) GUIUtility.ExitGUI();   // the rows below change: redraw from the next layout
            }

            if (_timelineSource == 0)
            {
                BuildCrumbRows(report);
                if (_crumbRows.Count == 0) { EditorGUILayout.HelpBox("No breadcrumbs match.", MessageType.None); return; }
                VirtualRows(ref _rowsScroll, _crumbRows.Count, position.height - 90, (i, rect) =>
                {
                    var crumb = _crumbRows[i];
                    if (i == _selectedCrumb) EditorGUI.DrawRect(rect, SelectedColor);
                    RowText(rect, crumb.Time, crumb.Frame.ToString(CultureInfo.InvariantCulture), crumb.Category,
                            string.IsNullOrEmpty(crumb.Scene) ? crumb.Text : $"{crumb.Text}   @{crumb.Scene}", 0);
                    if (Clicked(rect)) _selectedCrumb = i;
                });
                return;
            }

            var log = report.SessionLogs[_timelineSource - 1];
            BuildLogRows(log, timeline: true);
            float listHeight = _selectedEntry != null ? (position.height - 90) * 0.55f : position.height - 90;
            if (_rows.Count == 0) EditorGUILayout.HelpBox("Nothing matches.", MessageType.None);
            else VirtualRows(ref _rowsScroll, _rows.Count, listHeight, (i, rect) => DrawEntryRow(_rows[i], log.Name, rect));
            if (_selectedEntry != null) DrawEntryDetails(_selectedEntry, _selectedEntryLog, null);
        }

        private void DrawProblems(InspectedReport report)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(report.Problems.Count == 0 ? "No warnings or errors"
                                : $"{report.Problems.Count} different warning(s)/error(s), most severe first", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(report.ProblemsText)))
                {
                    bool show = GUILayout.Toggle(_showProblemsText, "problems.txt", EditorStyles.toolbarButton);
                    if (show != _showProblemsText) { _showProblemsText = show; GUIUtility.ExitGUI(); }
                }
            }

            if (_showProblemsText)
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_pageScroll))
                {
                    _pageScroll = scroll.scrollPosition;
                    Wrapped(report.ProblemsText);
                }
                return;
            }
            if (report.Problems.Count == 0)
            {
                EditorGUILayout.HelpBox("This report's session logs hold no warnings or errors.", MessageType.Info);
                return;
            }

            float listHeight = Mathf.Min(report.Problems.Count * RowHeight + 4, (position.height - 90) * 0.45f);
            VirtualRows(ref _rowsScroll, report.Problems.Count, listHeight, (i, rect) =>
            {
                var group = report.Problems[i];
                if (i == _selectedGroup) EditorGUI.DrawRect(rect, SelectedColor);
                GroupRow(rect, group, $"×{group.Occurrences}");
                if (Clicked(rect)) { _selectedGroup = i; _detailScroll = Vector2.zero; GUIUtility.ExitGUI(); }
            });
            if (_selectedGroup < 0 || _selectedGroup >= report.Problems.Count) return;
            var selected = report.Problems[_selectedGroup];
            DrawEntryDetails(selected.Sample, selected.SampleLog,
                string.Format(CultureInfo.InvariantCulture, "{0} time(s), first at {1}, last at {2}, in {3}",
                              selected.Occurrences, Clock(selected.FirstTime), Clock(selected.LastTime),
                              string.Join(", ", selected.Logs)));
        }

        private void DrawLogs(InspectedReport report)
        {
            if (report.SessionLogs.Count == 0)
            {
                EditorGUILayout.HelpBox("This report has no session log.", MessageType.Info);
                return;
            }
            _logIndex = Mathf.Clamp(_logIndex, 0, report.SessionLogs.Count - 1);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int index = EditorGUILayout.Popup(_logIndex, report.SessionLogs.Select(l => l.Name).ToArray(),
                                                  EditorStyles.toolbarPopup, GUILayout.Width(260));
                if (index != _logIndex) { _logIndex = index; _selectedEntry = null; GUIUtility.ExitGUI(); }
                EditorGUI.BeginChangeCheck();
                _showInfo = GUILayout.Toggle(_showInfo, "Info", EditorStyles.toolbarButton);
                _showCrumbs = GUILayout.Toggle(_showCrumbs, "Crumbs", EditorStyles.toolbarButton);
                _showNotes = GUILayout.Toggle(_showNotes, "Notes", EditorStyles.toolbarButton);
                _showWarnings = GUILayout.Toggle(_showWarnings, "Warnings", EditorStyles.toolbarButton);
                _showErrors = GUILayout.Toggle(_showErrors, "Errors", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                _logFilter = EditorGUILayout.TextField(_logFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck()) GUIUtility.ExitGUI();   // the rows below change: redraw from the next layout
            }

            var log = report.SessionLogs[_logIndex];
            string status = log.End ?? "No end line: this run crashed, was killed, or was still running";
            if (log.Cuts > 0) status += $"  ·  the size limit removed older lines in {log.Cuts} place(s)";
            GUILayout.Label($"{status}  ·  {log.Entries.Count} entries", EditorStyles.miniLabel);
            if (Foldout("header:" + log.Name, false, "Header (the app and PC when this session started)"))
            {
                EditorGUI.indentLevel++;
                foreach (var (key, value) in log.Header) Field(key, value);
                EditorGUI.indentLevel--;
            }

            BuildLogRows(log, timeline: false);
            float listHeight = _selectedEntry != null ? (position.height - 120) * 0.55f : position.height - 120;
            if (_rows.Count == 0) EditorGUILayout.HelpBox("Nothing matches.", MessageType.None);
            else VirtualRows(ref _rowsScroll, _rows.Count, listHeight, (i, rect) => DrawEntryRow(_rows[i], log.Name, rect));
            if (_selectedEntry != null) DrawEntryDetails(_selectedEntry, _selectedEntryLog, null);
        }

        private void DrawFiles(InspectedReport report)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Everything in the zip. \"Open\" unpacks it into the project's Temp folder.", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Unpack All", EditorStyles.toolbarButton))
                {
                    string folder = TempFolder(report);
                    try { report.ExtractAll(folder); EditorUtility.RevealInFinder(folder); }
                    catch (Exception e) { ShowNotification(new GUIContent("Couldn't unpack: " + e.Message)); }
                }
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(_pageScroll))
            {
                _pageScroll = scroll.scrollPosition;
                foreach (var file in report.Files)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(file.Path, GUILayout.Width(280));
                        string size = Size(file.WrittenBytes);
                        if (file.SourceBytes > file.WrittenBytes) size += " of " + Size(file.SourceBytes);
                        GUILayout.Label(size, EditorStyles.miniLabel, GUILayout.Width(120));
                        using (new EditorGUI.DisabledScope(file.WrittenBytes == 0))
                            if (GUILayout.Button("Open", GUILayout.Width(50))) OpenExtracted(report, file.Path);
                        GUILayout.Label(file.Note ?? string.Empty, EditorStyles.miniLabel);
                    }
                }
            }
        }

        // ── Across reports ──────────────────────────────────────────────────────

        private void DrawAcross()
        {
            GUILayout.Label($"All {_reports.Count} reports", _title);
            GUILayout.Label("Warnings and errors grouped by message and top stack frames, across every loaded report. " +
                            "Pick one to see which reports have it and what those reports share.", _wrapped);
            if (_crossGroups.Count == 0)
            {
                EditorGUILayout.HelpBox("None of the loaded reports has a warning or an error.", MessageType.Info);
                return;
            }

            float listHeight = Mathf.Min(_crossGroups.Count * RowHeight + 4, (position.height - 110) * 0.4f);
            VirtualRows(ref _rowsScroll, _crossGroups.Count, listHeight, (i, rect) =>
            {
                var group = _crossGroups[i];
                if (i == _selectedCross) EditorGUI.DrawRect(rect, SelectedColor);
                GroupRow(rect, group, $"{group.Reports.Count} of {_reports.Count} · ×{group.Occurrences}");
                if (Clicked(rect)) { _selectedCross = i; _detailScroll = Vector2.zero; GUIUtility.ExitGUI(); }
            });
            if (_selectedCross < 0 || _selectedCross >= _crossGroups.Count) return;

            var selected = _crossGroups[_selectedCross];
            using (var scroll = new EditorGUILayout.ScrollViewScope(_pageScroll, GUILayout.Height((position.height - 110) * 0.3f)))
            {
                _pageScroll = scroll.scrollPosition;
                GUILayout.Label("Reports with it", EditorStyles.boldLabel);
                foreach (var report in selected.Reports)
                {
                    if (!EditorGUILayout.LinkButton($"{report.Title}   {report.CreatedLocal}   {report.DescriptionFirstLine}")) continue;
                    _jumpTo = (report, selected.Signature);   // applied before the next frame's layout
                    Repaint();
                }

                EditorGUILayout.Space(4);
                GUILayout.Label("What those reports share", EditorStyles.boldLabel);
                foreach (var (section, key) in CompareFields) Field(key, Shared(selected.Reports, section, key));
            }
            DrawEntryDetails(selected.Sample, selected.SampleLog, null);
        }

        /// <summary>"all: X" when every report in the group has the same value, else the values with their counts;
        /// plus how many different values all loaded reports have, for contrast.</summary>
        private string Shared(List<InspectedReport> group, string section, string key)
        {
            var counts = group.Select(r => Blank(r.Field(section, key))).GroupBy(v => v)
                              .OrderByDescending(g => g.Count()).ToList();
            int overall = _reports.Where(r => r.LoadError == null).Select(r => r.Field(section, key)).Distinct().Count();
            string text = counts.Count == 1 && group.Count > 1
                ? $"all {group.Count}: {counts[0].Key}"
                : string.Join("  ·  ", counts.Select(g => g.Count() > 1 ? $"{g.Key} (×{g.Count()})" : g.Key));
            return overall > 1 ? $"{text}      [all loaded reports: {overall} different]" : text;
        }

        // ── Rows and details ────────────────────────────────────────────────────

        private void BuildCrumbRows(InspectedReport report)
        {
            string key = $"crumbs|{report.SourcePath}|{_timelineFilter}";
            if (key == _rowsKey) return;
            _rowsKey = key;
            _crumbRows.Clear();
            foreach (var crumb in report.Breadcrumbs)
                if (Matches(_timelineFilter, crumb.Category, crumb.Text, crumb.Scene)) _crumbRows.Add(crumb);
        }

        private void BuildLogRows(SessionLogFile log, bool timeline)
        {
            string key = timeline
                ? $"timeline|{log.Name}|{_selected}|{_timelineProblems}|{_timelineFilter}"
                : $"log|{log.Name}|{_selected}|{_showInfo}{_showCrumbs}{_showNotes}{_showWarnings}{_showErrors}|{_logFilter}";
            if (key == _rowsKey) return;
            _rowsKey = key;
            _rows.Clear();
            foreach (var entry in log.Entries)
            {
                bool shown;
                if (timeline)
                    shown = entry.Level == LogEntry.Crumb || entry.Level == LogEntry.Note || entry.Level == LogEntry.Cut
                            || (_timelineProblems && entry.IsProblem);
                else
                    shown = entry.Severity == 2 ? _showErrors
                          : entry.Severity == 1 ? _showWarnings
                          : entry.Level == LogEntry.Crumb ? _showCrumbs
                          : entry.Level == LogEntry.Info ? _showInfo
                          : _showNotes;   // notes, cut markers, unreadable lines
                if (shown && Matches(timeline ? _timelineFilter : _logFilter, entry.Tag, entry.Message, null)) _rows.Add(entry);
            }
        }

        private void DrawEntryRow(LogEntry entry, string logName, Rect rect)
        {
            if (entry == _selectedEntry) EditorGUI.DrawRect(rect, SelectedColor);
            string text = entry.Occurrences > 1 ? $"{entry.Message}   (×{entry.Occurrences})" : entry.Message;
            if (entry.Tag != null) text = $"[{entry.Tag}] {text}";
            RowText(rect, entry.Time, entry.Frame, entry.Level, text, entry.Severity);
            if (!Clicked(rect)) return;
            _selectedEntry = entry;
            _selectedEntryLog = logName;
            _detailScroll = Vector2.zero;
            if (Event.current.clickCount == 2) OpenFirstFrame(entry);
            GUIUtility.ExitGUI();   // the details panel appears: redraw from the next layout
        }

        private void RowText(Rect rect, double time, string frame, string kind, string text, int severity)
        {
            var color = GUI.color;
            if (severity == 2) GUI.color = ErrorColor;
            else if (severity == 1) GUI.color = WarningColor;
            GUI.Label(new Rect(rect.x + 4, rect.y, 78, rect.height), Clock(time), EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 82, rect.y, 60, rect.height), "f" + frame, EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 142, rect.y, 78, rect.height), kind, EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(rect.x + 220, rect.y, rect.width - 224, rect.height), text, EditorStyles.label);
            GUI.color = color;
        }

        private void GroupRow(Rect rect, ProblemGroup group, string counts)
        {
            var color = GUI.color;
            GUI.color = group.Severity == 2 ? ErrorColor : WarningColor;
            GUI.Label(new Rect(rect.x + 4, rect.y, 78, rect.height), group.Sample.Level, EditorStyles.miniBoldLabel);
            GUI.color = color;
            GUI.Label(new Rect(rect.x + 82, rect.y, 150, rect.height), counts, EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 232, rect.y, rect.width - 236, rect.height), group.Sample.Message, EditorStyles.label);
        }

        private void DrawEntryDetails(LogEntry entry, string logName, string extra)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), SeparatorColor);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll, GUILayout.ExpandHeight(true)))
            {
                _detailScroll = scroll.scrollPosition;
                using (new EditorGUILayout.HorizontalScope())
                {
                    string where = string.Format(CultureInfo.InvariantCulture, "{0}   at {1}, frame {2}{3}   {4}, line {5}",
                        entry.Level, Clock(entry.Time), entry.Frame, entry.Tag != null ? $"   [{entry.Tag}]" : string.Empty,
                        logName, entry.Line);
                    GUILayout.Label(where, EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50)))
                        EditorGUIUtility.systemCopyBuffer = entry.Message + "\n" + string.Join("\n", entry.Details);
                }
                if (extra != null) GUILayout.Label(extra, EditorStyles.miniLabel);
                Wrapped(entry.Message);
                foreach (var line in entry.Details)
                {
                    if (StackFrames.TryParse(line, out var frame) && !frame.IsLoggingCall)
                    {
                        if (EditorGUILayout.LinkButton(line) && !StackFrames.Open(frame))
                            ShowNotification(new GUIContent($"No script called {frame.ClassName} in this project"));
                    }
                    else GUILayout.Label(line, _detail);
                }
            }
        }

        private void OpenFirstFrame(LogEntry entry)
        {
            foreach (var line in entry.Details)
                if (StackFrames.TryParse(line, out var frame) && !frame.IsLoggingCall && StackFrames.Open(frame)) return;
        }

        private static void VirtualRows(ref Vector2 scroll, int count, float height, Action<int, Rect> drawRow)
        {
            height = Mathf.Max(60f, height);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            Rect all = GUILayoutUtility.GetRect(10f, Mathf.Max(1f, count * RowHeight), GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Layout)
            {
                int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
                int last = Mathf.Min(count - 1, Mathf.CeilToInt((scroll.y + height) / RowHeight));
                for (int i = first; i <= last; i++)
                    drawRow(i, new Rect(all.x, all.y + i * RowHeight, all.width, RowHeight));
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Small helpers ───────────────────────────────────────────────────────

        private void Field(string label, string value)
        {
            float width = Mathf.Max(120f, position.width - ListWidth - LabelWidth - 60f);
            float height = _wrapped.CalcHeight(new GUIContent(value), width);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, _key, GUILayout.Width(LabelWidth));
                EditorGUILayout.SelectableLabel(value, _wrapped, GUILayout.Width(width), GUILayout.Height(height));
            }
        }

        private void Wrapped(string text)
        {
            float width = Mathf.Max(120f, position.width - ListWidth - 40f);
            EditorGUILayout.SelectableLabel(text, _wrapped, GUILayout.Width(width),
                                            GUILayout.Height(_wrapped.CalcHeight(new GUIContent(text), width)));
        }

        private bool Foldout(string id, bool openByDefault, string label = null)
        {
            if (!_foldouts.TryGetValue(id, out bool open)) open = openByDefault;
            open = EditorGUILayout.Foldout(open, label ?? id, true, EditorStyles.foldoutHeader);
            _foldouts[id] = open;
            return open;
        }

        private bool Clicked(Rect rect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition)) return false;
            e.Use();
            Repaint();
            return true;
        }

        private void OpenExtracted(InspectedReport report, string zipPath)
        {
            try
            {
                string folder = TempFolder(report);
                string file = Path.Combine(folder, zipPath);
                if (!File.Exists(file)) report.ExtractAll(folder);
                EditorUtility.OpenWithDefaultApp(file);
            }
            catch (Exception e)
            {
                ShowNotification(new GUIContent("Couldn't open it: " + e.Message));
            }
        }

        private static string TempFolder(InspectedReport report)
        {
            string name = string.Concat(report.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return Path.Combine(Path.GetFullPath("Temp"), "ReportInspector", name);
        }

        private static bool Matches(string filter, string a, string b, string c)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return Contains(a, filter) || Contains(b, filter) || Contains(c, filter);
        }

        private static bool Contains(string text, string filter) =>
            text != null && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Blank(string value) => string.IsNullOrEmpty(value) ? "(not in the report)" : value;

        /// <summary>Seconds since the session started, as m:ss.fff (h:mm:ss.fff past an hour).</summary>
        private static string Clock(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:000}", (int)time.TotalHours, time.Minutes, time.Seconds, time.Milliseconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:000}", time.Minutes, time.Seconds, time.Milliseconds);
        }

        private static string Size(long bytes) =>
            bytes >= 1024 * 1024 ? (bytes / (1024.0 * 1024.0)).ToString("0.0 MB", CultureInfo.InvariantCulture)
            : bytes >= 1024 ? (bytes / 1024.0).ToString("0.0 KB", CultureInfo.InvariantCulture)
            : bytes.ToString(CultureInfo.InvariantCulture) + " B";

        private static string LastFolder
        {
            get => EditorPrefs.GetString(PrefFolder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            set => EditorPrefs.SetString(PrefFolder, value);
        }

        private static Color SelectedColor => EditorGUIUtility.isProSkin ? new Color(0.24f, 0.37f, 0.59f, 0.8f) : new Color(0.58f, 0.73f, 0.94f, 0.8f);
        private static Color SeparatorColor => EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.6f, 0.6f, 0.6f);
        private static Color ErrorColor => EditorGUIUtility.isProSkin ? new Color(1f, 0.5f, 0.45f) : new Color(0.72f, 0.08f, 0.05f);
        private static Color WarningColor => EditorGUIUtility.isProSkin ? new Color(1f, 0.82f, 0.4f) : new Color(0.55f, 0.4f, 0f);

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, margin = new RectOffset(4, 4, 6, 4) };
            _wrapped = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _key = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            _detail = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _rightMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            _errorMini = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ErrorColor } };
        }
    }
}
