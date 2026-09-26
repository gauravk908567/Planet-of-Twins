#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PoT.Diagnostics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a real bug-report zip the way the report screen will (game.md §27.7 phase 2), then checks it:
///   • report.json parses, has format <c>pot-report/1</c> and the right report id;
///   • no text file in the zip still holds the Windows user name, the profile path or the machine name.
/// Best run while playing from Bootstrap, so the game sections (settings, input, save, world) have their
/// managers. With Play stopped they report "not loaded", and logs/ holds the last two sessions.
///
/// Run: <b>Planet of Twins Tools ▸ Diagnostics ▸ Build Test Report</b>. The zip is opened in Explorer.
/// </summary>
public static class DiagnosticsTestReport
{
    private static readonly string[] TextExtensions = { ".log", ".txt", ".json" };

    [Serializable]
    private class JsonProbe
    {
        public string format;
        public string reportId;
    }

    [MenuItem("Planet of Twins Tools/Diagnostics/Build Test Report")]
    public static void Build()
    {
        var request = new ReportRequest
        {
            Description = "Test report from the Editor (Planet of Twins Tools ▸ Diagnostics ▸ Build Test Report).",
            Category = "Other",
            Sections = PoTReportSections.All,
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        string path = ReportCollector.BuildZip(request, out var package);
        watch.Stop();

        var problems = new List<string>();
        int scanned = Verify(path, package.ReportId, problems);
        CheckTrimming(problems);

        var summary = new StringBuilder();
        summary.AppendFormat(CultureInfo.InvariantCulture, "[Diagnostics report] {0}: {1:0.0} KB zip in {2} ms{3}\n{4}\n",
            package.ReportId, new FileInfo(path).Length / 1024.0, watch.ElapsedMilliseconds,
            Application.isPlaying ? string.Empty : " (Play is stopped: the game sections say 'not loaded')", path);
        foreach (var file in package.Files)
        {
            summary.AppendFormat(CultureInfo.InvariantCulture, "  {0}  {1}", file.ZipPath, Size(file.WrittenBytes));
            if (file.Note != null)
            {
                if (file.SourceBytes > 0) summary.Append(" of ").Append(Size(file.SourceBytes));
                summary.Append("  (").Append(file.Note).Append(')');
            }
            summary.Append('\n');
        }

        if (problems.Count == 0)
        {
            summary.AppendFormat(CultureInfo.InvariantCulture,
                "Self-check PASS: report.json is valid, none of the {0} text files holds the user name, " +
                "the profile path or the machine name, and a 4 MB log is cut to its start and end on whole lines.", scanned);
            Debug.Log(summary.ToString());
        }
        else
        {
            summary.Append("Self-check FAIL:\n  ").Append(string.Join("\n  ", problems));
            Debug.LogError(summary.ToString());
        }
        EditorUtility.RevealInFinder(path);
    }

    /// <summary>Checks the zip; adds each problem found. Returns how many text files were scanned.</summary>
    private static int Verify(string zipPath, string reportId, List<string> problems)
    {
        var leaks = LeakPatterns();
        int scanned = 0;
        bool hasJson = false;

        using (var zip = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (!IsText(entry.FullName)) continue;
                string text;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) text = reader.ReadToEnd();
                scanned++;

                if (entry.FullName == ReportPackage.JsonName)
                {
                    hasJson = true;
                    CheckJson(text, reportId, problems);
                }
                foreach (var (label, pattern) in leaks)
                {
                    int hits = pattern.Matches(text).Count;
                    if (hits > 0) problems.Add($"{entry.FullName}: {hits} × {label}");
                }
            }
        }
        if (!hasJson) problems.Add($"{ReportPackage.JsonName} is missing");
        return scanned;
    }

    private static void CheckJson(string json, string reportId, List<string> problems)
    {
        try
        {
            var probe = JsonUtility.FromJson<JsonProbe>(json);
            if (probe == null || probe.format != ReportPackage.Format)
                problems.Add($"report.json format is '{probe?.format}', expected '{ReportPackage.Format}'");
            else if (probe.reportId != reportId)
                problems.Add($"report.json id is '{probe.reportId}', expected '{reportId}'");
        }
        catch (Exception e)
        {
            problems.Add($"report.json does not parse: {e.Message}");
        }
    }

    // A synthetic 4 MB log (every line ends in a 2-byte UTF-8 'æ') must come out under the limit with its first and
    // last lines, the cut marker, and only whole lines. Real logs are rarely that long in the Editor.
    private static void CheckTrimming(List<string> problems)
    {
        const int lineCount = 40000;
        string folder = Path.Combine(Path.GetTempPath(), "PoT_ReportTrimCheck");
        string log = Path.Combine(folder, "trim_check.log");
        string zipPath = Path.Combine(folder, "trim_check.zip");
        try
        {
            Directory.CreateDirectory(folder);
            using (var writer = new StreamWriter(log, false, new UTF8Encoding(false)))
                for (int i = 1; i <= lineCount; i++) writer.Write(TrimCheckLine(i) + "\n");

            var package = ReportCollector.Collect(new ReportRequest
            {
                Description = "Trim check (synthetic)",
                Sections = new IReportSection[] { new SingleFileSection("logs/trim_check.log", log) },
            });
            package.WriteZip(zipPath);

            string text;
            using (var zip = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read))
            using (var reader = new StreamReader(zip.GetEntry("logs/trim_check.log").Open(), Encoding.UTF8))
                text = reader.ReadToEnd();

            if (Encoding.UTF8.GetByteCount(text) > ReportPackage.TextFileLimit + 512)
                problems.Add($"trim: {Encoding.UTF8.GetByteCount(text)} bytes, over the {ReportPackage.TextFileLimit} limit");
            if (!text.StartsWith(TrimCheckLine(1) + "\n", StringComparison.Ordinal))
                problems.Add("trim: the first line is missing");
            if (!text.EndsWith(TrimCheckLine(lineCount) + "\n", StringComparison.Ordinal))
                problems.Add("trim: the last line is missing");
            if (text.IndexOf("bytes cut here", StringComparison.Ordinal) < 0)
                problems.Add("trim: no cut marker");
            var whole = new Regex(@"^line \d{5} [.]{80} æ$");
            foreach (var line in text.Split('\n'))
                if (line.StartsWith("line ", StringComparison.Ordinal) && !whole.IsMatch(line))
                {
                    problems.Add($"trim: a broken line: '{line}'");
                    break;
                }
        }
        catch (Exception e)
        {
            problems.Add($"trim: the check failed to run: {e.Message}");
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (Exception) { /* temp folder: leave it */ }
        }
    }

    private static string TrimCheckLine(int i) =>
        "line " + i.ToString("00000", CultureInfo.InvariantCulture) + " " + new string('.', 80) + " æ";

    private sealed class SingleFileSection : IReportSection
    {
        private readonly string _zipPath, _source;
        public SingleFileSection(string zipPath, string source) { _zipPath = zipPath; _source = source; }
        public string Name => "trimcheck";
        public void Collect(ReportSectionBuilder section) => section.AddTextFile(_zipPath, _source);
    }

    // What must not survive redaction: the profile path (either slash) and the user and machine names as words.
    private static List<(string Label, Regex Pattern)> LeakPatterns()
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var patterns = new List<(string, Regex)>();
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        if (profile.Length >= 3)
        {
            patterns.Add(("profile path", new Regex(Regex.Escape(profile), options)));
            patterns.Add(("profile path (/)", new Regex(Regex.Escape(profile.Replace('\\', '/')), options)));
        }
        AddWord(patterns, "user name", Environment.UserName, options);
        AddWord(patterns, "machine name", Environment.MachineName, options);
        return patterns;
    }

    private static void AddWord(List<(string, Regex)> patterns, string label, string word, RegexOptions options)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 3) return;   // the redactor skips these too
        patterns.Add((label, new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(word) + @"(?![\p{L}\p{N}])", options)));
    }

    private static bool IsText(string zipPath)
    {
        string extension = Path.GetExtension(zipPath);
        foreach (var text in TextExtensions)
            if (string.Equals(extension, text, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string Size(long bytes) =>
        bytes >= 1024 * 1024
            ? (bytes / (1024.0 * 1024.0)).ToString("0.0 MB", CultureInfo.InvariantCulture)
            : (bytes / 1024.0).ToString("0.0 KB", CultureInfo.InvariantCulture);
}
#endif
