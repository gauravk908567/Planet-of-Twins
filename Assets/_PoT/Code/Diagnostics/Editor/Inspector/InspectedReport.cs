using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PoT.Diagnostics.Editor
{
    /// <summary>
    /// One report zip, read the way the Report Inspector needs it: <c>report.json</c> (format <c>pot-report/1</c>),
    /// the session logs under <c>logs/</c> and <c>crash/</c>, <c>logs/problems.txt</c> and the screenshot. It reads
    /// only the documented format, so it works for any game that uses the diagnostics package. A zip that can't be
    /// read still loads, with <see cref="LoadError"/> set.
    /// </summary>
    public sealed class InspectedReport
    {
        public sealed class Section
        {
            public string Name;
            public readonly List<(string Key, string Value)> Fields = new List<(string, string)>();
        }

        public struct Crumb
        {
            public double Time;
            public long Frame;
            public string Category, Text, Scene;
        }

        public struct FileRow
        {
            public string Path;
            public long SourceBytes, WrittenBytes;
            public string Note;
        }

        public const string ProblemsPath = "logs/problems.txt";
        public const string SavePrefix = "save/";
        private const string JsonName = "report.json";
        private const string ScreenshotName = "screenshot.jpg";

        public string SourcePath { get; private set; }
        /// <summary>Why the zip couldn't be read; null when it loaded.</summary>
        public string LoadError { get; private set; }

        public string Format = string.Empty, ReportId = string.Empty, CreatedUtc = string.Empty;
        public string Description = string.Empty, Category = string.Empty, Contact = string.Empty;
        public readonly List<Section> Sections = new List<Section>();
        /// <summary>report.json's trail: the run that made the report.</summary>
        public readonly List<Crumb> Breadcrumbs = new List<Crumb>();
        public readonly List<FileRow> Files = new List<FileRow>();
        /// <summary>Newest session log first; a crashed run's log (<c>crash/</c>) last.</summary>
        public readonly List<SessionLogFile> SessionLogs = new List<SessionLogFile>();
        public string ProblemsText = string.Empty;
        public byte[] Screenshot;
        /// <summary>This report's warnings and errors, grouped (see <see cref="ProblemGroup"/>).</summary>
        public List<ProblemGroup> Problems = new List<ProblemGroup>();

        /// <summary>The id, or the file name when the report has none.</summary>
        public string Title => string.IsNullOrEmpty(ReportId) ? Path.GetFileNameWithoutExtension(SourcePath) : ReportId;

        public int ErrorCount => Count(2);
        public int WarningCount => Count(1);

        /// <summary>When the report was made, in local time ("" when unknown).</summary>
        public string CreatedLocal =>
            DateTime.TryParse(CreatedUtc, CultureInfo.InvariantCulture,
                              DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
                ? utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : CreatedUtc;

        /// <summary>A field's value, e.g. <c>Field("system", "GPU")</c>; "" when missing.</summary>
        public string Field(string section, string key)
        {
            foreach (var s in Sections)
            {
                if (s.Name != section) continue;
                foreach (var (k, v) in s.Fields)
                    if (k == key) return v;
            }
            return string.Empty;
        }

        /// <summary>The first line of the player's description.</summary>
        public string DescriptionFirstLine
        {
            get
            {
                string text = Description.Trim();
                int newline = text.IndexOf('\n');
                return newline >= 0 ? text.Substring(0, newline).TrimEnd('\r') : text;
            }
        }

        /// <summary>Reads one file out of the zip.</summary>
        public byte[] ReadEntry(string zipPath)
        {
            using (var zip = OpenZip(SourcePath))
            {
                var entry = zip.GetEntry(zipPath) ?? throw new FileNotFoundException($"'{zipPath}' is not in the report.");
                using (var stream = entry.Open())
                using (var copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                    return copy.ToArray();
                }
            }
        }

        /// <summary>Unpacks the whole zip into <paramref name="folder"/> (replacing what's there).</summary>
        public void ExtractAll(string folder)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            using (var zip = OpenZip(SourcePath))
            {
                string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;   // a folder entry
                    string target = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;   // "../" in a name
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var source = entry.Open())
                    using (var file = File.Create(target))
                        source.CopyTo(file);
                }
            }
        }

        public static InspectedReport Load(string zipPath)
        {
            var report = new InspectedReport { SourcePath = zipPath };
            try
            {
                using (var zip = OpenZip(zipPath))
                {
                    bool hasJson = false;
                    foreach (var entry in zip.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');
                        if (name == JsonName) { report.ReadJson(ReadText(entry)); hasJson = true; }
                        else if (name == ProblemsPath) report.ProblemsText = ReadText(entry);
                        else if (name == ScreenshotName) report.Screenshot = ReadBytes(entry);
                        else if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                        {
                            string text = ReadText(entry);
                            if (SessionLogParser.IsSessionLog(text)) report.SessionLogs.Add(SessionLogParser.Parse(name, text));
                        }
                    }
                    if (!hasJson) report.LoadError = $"not a bug report: it has no {JsonName}";
                }
            }
            catch (Exception e)
            {
                report.LoadError = e is InvalidDataException ? "not a zip file" : e.Message;
            }

            // Newest session log first (the names hold the start time); a crashed run's log last.
            report.SessionLogs.Sort((a, b) =>
            {
                bool aCrash = a.Name.StartsWith("crash/", StringComparison.Ordinal);
                bool bCrash = b.Name.StartsWith("crash/", StringComparison.Ordinal);
                if (aCrash != bCrash) return aCrash ? 1 : -1;
                return string.CompareOrdinal(b.Name, a.Name);
            });
            report.Problems = ProblemGroup.GroupAll(report.SessionLogs);
            return report;
        }

        private void ReadJson(string text)
        {
            if (!(JsonLite.Parse(text) is JsonObject root)) throw new FormatException("report.json is not an object");
            Format = root.GetString("format");
            ReportId = root.GetString("reportId");
            CreatedUtc = root.GetString("createdUtc");
            Description = root.GetString("description");
            Category = root.GetString("category");
            Contact = root.GetString("contact");

            var sections = root.GetObject("sections");
            if (sections != null)
            {
                foreach (var pair in sections)
                {
                    var section = new Section { Name = pair.Key };
                    if (pair.Value is JsonObject fields)
                        foreach (var field in fields) section.Fields.Add((field.Key, fields.GetString(field.Key)));
                    Sections.Add(section);
                }
            }

            foreach (var item in root.GetArray("breadcrumbs") ?? new List<object>())
            {
                if (!(item is JsonObject crumb)) continue;
                Breadcrumbs.Add(new Crumb
                {
                    Time = crumb.GetNumber("time"),
                    Frame = (long)crumb.GetNumber("frame"),
                    Category = crumb.GetString("category"),
                    Text = crumb.GetString("text"),
                    Scene = crumb.GetString("scene"),
                });
            }

            foreach (var item in root.GetArray("files") ?? new List<object>())
            {
                if (!(item is JsonObject file)) continue;
                Files.Add(new FileRow
                {
                    Path = file.GetString("path"),
                    SourceBytes = (long)file.GetNumber("sourceBytes"),
                    WrittenBytes = (long)file.GetNumber("writtenBytes"),
                    Note = file.Get("note") as string,
                });
            }
        }

        private int Count(int severity)
        {
            int count = 0;
            foreach (var group in Problems)
                if (group.Severity == severity) count += group.Occurrences;
            return count;
        }

        private static ZipArchive OpenZip(string path)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try { return new ZipArchive(stream, ZipArchiveMode.Read); }
            catch
            {
                stream.Dispose();   // not a zip: don't keep the file open
                throw;
            }
        }

        private static string ReadText(ZipArchiveEntry entry)
        {
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) return reader.ReadToEnd();
        }

        private static byte[] ReadBytes(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }
    }
}
