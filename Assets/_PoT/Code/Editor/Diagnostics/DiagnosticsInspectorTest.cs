using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PoT.Diagnostics;
using PoT.Diagnostics.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Self-test for the Report Inspector (game.md §27.9 phase 6):
///   • the session-log parser: header, entries, tags, repeats, held-back copies, the trim marker, the end line;
///   • problem grouping: the same warning with different numbers is one group, counted with its repeats;
///   • stack frames: a build machine's path and a class name with no path both find the project's script;
///   • report.json of a real report built now (the id, format, sections, files), plus a zip that isn't a report
///     and a file that isn't a zip;
///   • "Load this save": the handler is found and knows which files are save slots (nothing is loaded).
/// Run: <b>Planet of Twins Tools ▸ Diagnostics ▸ Test Report Inspector</b>. On PASS the window opens on the
/// report it built.
/// </summary>
public static class DiagnosticsInspectorTest
{
    private const string SyntheticLog =
        "=== Session log ===\n" +
        "Product: Test\n" +
        "Version: 1.0\n" +
        "===\n" +
        "000000.100 f1       CRUMB     [Flow] boot: front-end  @Bootstrap\n" +
        "000001.500 f30      INFO      [AI] Grunt_12 mood → Angry (duration=3.0s)\n" +
        "000002.000 f40      WARNING   Enemy 12 lost its target\n" +
        "    UnityEngine.Debug:LogWarning (object)\n" +
        "    EnemySpawner:SpawnOne () (at Assets/_PoT/Code/Enemies/Spawn/EnemySpawner.cs:337)\n" +
        "    (previous line repeated 4 more times)\n" +
        "=== 1.2 MB of older lines removed here (1 trim(s)): this log keeps its first 1 MB and its newest 4 MB ===\n" +
        "000009.000 f90      WARNING   Enemy 7 lost its target\n" +
        "    UnityEngine.Debug:LogWarning (object)\n" +
        "    EnemySpawner:SpawnOne () (at Assets/_PoT/Code/Enemies/Spawn/EnemySpawner.cs:337)\n" +
        "    (3 copies of this line in the 2.5 s before it were held back: the log keeps 20 per 10 s of each message)\n" +
        "000010.000 f95      ERROR     Something broke\n" +
        "    second line of the message\n" +
        "    UnityEngine.Debug:LogError (object)\n" +
        "    SaveService:Continue (int) (at C:/BuildMachine/Project/Assets/_PoT/Code/Progression/Save/SaveService.cs:109)\n" +
        "000011.000 f-       NOTE      Problems this session: 2 different\n" +
        "    WARNING ×10, 3 held back  first 2.0  last 9.0\n" +
        "=== Session end (clean quit) at 12.000s ===\n";

    [MenuItem("Planet of Twins Tools/Diagnostics/Test Report Inspector")]
    public static void Run()
    {
        var problems = new List<string>();
        CheckJson(problems);
        CheckParser(problems);
        CheckFrames(problems);
        string zip = CheckRealReport(problems);
        CheckNotReports(problems);
        CheckSaveHandler(problems);

        if (problems.Count == 0)
        {
            Debug.Log("[Report Inspector test] PASS: the parser, grouping, stack frames, a real report and the save " +
                      $"handler all check out.\n{zip}");
            if (zip != null) ReportInspectorWindow.Open(new[] { zip });
        }
        else Debug.LogError("[Report Inspector test] FAIL:\n  " + string.Join("\n  ", problems));
    }

    private static void CheckJson(List<string> problems)
    {
        try
        {
            var root = (JsonObject)JsonLite.Parse("{ \"b\": \"x\\n\\\"y\\\" \\u00e6\", \"a\": { \"k\": 1.5 }, \"list\": [true, null] }");
            Expect(problems, root[0].Key == "b" && root[1].Key == "a", "json: keys lost their file order");
            Expect(problems, root.GetString("b") == "x\n\"y\" æ", $"json: escapes read as '{root.GetString("b")}'");
            Expect(problems, root.GetObject("a")?.GetNumber("k") == 1.5, "json: nested number");
            Expect(problems, root.GetArray("list")?.Count == 2, "json: array");
        }
        catch (Exception e) { problems.Add("json: " + e.Message); }
    }

    private static void CheckParser(List<string> problems)
    {
        Expect(problems, SessionLogParser.IsSessionLog(SyntheticLog), "parser: the synthetic log isn't recognised");
        var log = SessionLogParser.Parse("logs/test.log", SyntheticLog);
        Expect(problems, log.Header.Count == 2 && log.HeaderValue("Version") == "1.0", $"parser: header has {log.Header.Count} lines");
        Expect(problems, log.Entries.Count == 7, $"parser: {log.Entries.Count} entries, expected 7");
        Expect(problems, log.Cuts == 1, $"parser: {log.Cuts} trim markers, expected 1");
        Expect(problems, log.End == "Session end (clean quit) at 12.000s", $"parser: end line '{log.End}'");
        if (log.Entries.Count != 7) return;

        var info = log.Entries[1];
        Expect(problems, info.Level == LogEntry.Info && info.Tag == "AI" && info.Message.StartsWith("Grunt_12", StringComparison.Ordinal),
               $"parser: INFO line read as {info.Level} [{info.Tag}] '{info.Message}'");
        Expect(problems, Math.Abs(info.Time - 1.5) < 1e-6 && info.Frame == "30", "parser: time/frame");
        Expect(problems, log.Entries[2].Repeats == 4, $"parser: repeats {log.Entries[2].Repeats}, expected 4");
        Expect(problems, log.Entries[4].HeldBack == 3, $"parser: held back {log.Entries[4].HeldBack}, expected 3");
        Expect(problems, log.Entries[5].Details.Count == 3, $"parser: the error has {log.Entries[5].Details.Count} detail lines");
        Expect(problems, log.Entries[6].Frame == "-" && log.Entries[6].Level == LogEntry.Note, "parser: the off-thread NOTE");

        var groups = ProblemGroup.GroupAll(new[] { log });
        Expect(problems, groups.Count == 2, $"grouping: {groups.Count} groups, expected 2 (one error, one warning)");
        if (groups.Count == 2)
        {
            Expect(problems, groups[0].Severity == 2, "grouping: the error isn't first");
            Expect(problems, groups[1].Occurrences == 9, $"grouping: the warning counts {groups[1].Occurrences}, expected 9 (1+4 and 1+3)");
            Expect(problems, Math.Abs(groups[1].FirstTime - 2) < 1e-6 && Math.Abs(groups[1].LastTime - 9) < 1e-6, "grouping: first/last time");
        }
    }

    private static void CheckFrames(List<string> problems)
    {
        const string buildFrame = "SaveService:Continue (int) (at C:/BuildMachine/Project/Assets/_PoT/Code/Progression/Save/SaveService.cs:109)";
        if (StackFrames.TryParse(buildFrame, out var frame))
        {
            Expect(problems, frame.Line == 109 && frame.ClassName == "SaveService", $"frames: read as {frame.ClassName}:{frame.Line}");
            Expect(problems, AssetDatabase.GetAssetPath(StackFrames.Resolve(frame)) == "Assets/_PoT/Code/Progression/Save/SaveService.cs",
                   "frames: a build machine's path doesn't find SaveService.cs");
        }
        else problems.Add("frames: a normal frame didn't parse");

        if (StackFrames.TryParse("EnemySpawner+<SpawnLoop>d__12:MoveNext ()", out var noPath))
            Expect(problems, StackFrames.Resolve(noPath) != null && noPath.ClassName == "EnemySpawner",
                   $"frames: a frame without a path doesn't find its class ({noPath.ClassName})");
        else problems.Add("frames: a coroutine frame didn't parse");

        Expect(problems, StackFrames.TryParse("UnityEngine.Debug:LogError (object)", out var logging) && logging.IsLoggingCall,
               "frames: Unity's logging call isn't recognised");
        Expect(problems, !StackFrames.TryParse("NullReferenceException: Object reference not set to an instance of an object", out _),
               "frames: an exception message was read as a frame");
        Expect(problems, !StackFrames.TryParse("second line of the message", out _), "frames: a message line was read as a frame");
    }

    private static string CheckRealReport(List<string> problems)
    {
        try
        {
            string zip = ReportCollector.BuildZip(new ReportRequest
            {
                Description = "Report Inspector self-test (Planet of Twins Tools ▸ Diagnostics ▸ Test Report Inspector).",
                Category = "Other",
                Sections = PoTReportSections.All,
            }, out var package);

            var report = InspectedReport.Load(zip);
            Expect(problems, report.LoadError == null, $"real report: {report.LoadError}");
            Expect(problems, report.ReportId == package.ReportId, $"real report: id '{report.ReportId}', expected '{package.ReportId}'");
            Expect(problems, report.Format == ReportPackage.Format, $"real report: format '{report.Format}'");
            Expect(problems, report.Category == "Other" && report.Description.StartsWith("Report Inspector self-test", StringComparison.Ordinal),
                   "real report: description / category");
            Expect(problems, !string.IsNullOrEmpty(report.Field("system", "GPU")) && !string.IsNullOrEmpty(report.Field("app", "Version")),
                   "real report: the app/system sections are missing fields");
            Expect(problems, report.Files.Count == package.Files.Count, $"real report: {report.Files.Count} files listed, expected {package.Files.Count}");
            Expect(problems, report.Files.Any(f => f.Path == InspectedReport.ProblemsPath), "real report: no logs/problems.txt");
            return zip;
        }
        catch (Exception e)
        {
            problems.Add("real report: " + e.Message);
            return null;
        }
    }

    private static void CheckNotReports(List<string> problems)
    {
        string folder = Path.Combine(Path.GetTempPath(), "PoT_InspectorCheck");
        try
        {
            Directory.CreateDirectory(folder);
            string notReport = Path.Combine(folder, "not_a_report.zip");
            using (var zip = new ZipArchive(File.Create(notReport), ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry("readme.txt").Open(), Encoding.UTF8))
                writer.Write("hello");
            string notZip = Path.Combine(folder, "not_a_zip.zip");
            File.WriteAllText(notZip, "just text");

            var first = InspectedReport.Load(notReport);
            Expect(problems, first.LoadError != null && first.LoadError.Contains("not a bug report"), $"not-a-report: '{first.LoadError}'");
            var second = InspectedReport.Load(notZip);
            Expect(problems, second.LoadError == "not a zip file", $"not-a-zip: '{second.LoadError}'");
        }
        catch (Exception e) { problems.Add("not-a-report checks: " + e.Message); }
        finally
        {
            try { Directory.Delete(folder, true); } catch (Exception) { /* temp folder: leave it */ }
        }
    }

    private static void CheckSaveHandler(List<string> problems)
    {
        Expect(problems, TypeCache.GetTypesDerivedFrom<IReportSaveHandler>().Contains(typeof(PoTReportSaveHandler)),
               "save handler: the Inspector can't find PoTReportSaveHandler");
        var handler = new PoTReportSaveHandler();
        string slot2 = InspectedReport.SavePrefix + Path.GetFileName(SaveSystem.PathFor(1));
        Expect(problems, handler.CanLoad(slot2) && handler.ButtonLabel(slot2) == "Load into Slot 2",
               $"save handler: '{slot2}' isn't offered as Slot 2");
        Expect(problems, !handler.CanLoad("save/other.json"), "save handler: accepts a file that isn't a slot");
        Expect(problems, !handler.CanLoad("logs/" + Path.GetFileName(SaveSystem.PathFor(0))), "save handler: accepts a file outside save/");
    }

    private static void Expect(List<string> problems, bool ok, string failure)
    {
        if (!ok) problems.Add(failure);
    }
}
