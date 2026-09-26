using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// A collected bug report, ready to be written as a zip (game.md §27.3):
    /// <c>report.json</c> (format <see cref="Format"/>) + <c>logs/</c> + <c>crash/</c> + the game's files +
    /// <c>screenshot.jpg</c>. Made on the main thread by <see cref="ReportCollector.Collect"/>. The files are read
    /// only by <see cref="WriteZip"/>, which uses no Unity API, so it can run on a worker thread.
    ///
    /// report.json: <c>format, reportId, createdUtc, description, category, contact, sections{name:{key:value}},
    /// breadcrumbs[{time, frame, category, text, scene}], files[{path, sourceBytes, writtenBytes, note}]</c>.
    /// It is written last, so its file list says what really went into the zip.
    /// </summary>
    public sealed class ReportPackage
    {
        public const string Format = "pot-report/1";
        public const string JsonName = "report.json";
        public const string ScreenshotName = "screenshot.jpg";

        /// <summary>A longer text file keeps its first <see cref="TextHeadBytes"/> and its end, up to this size.</summary>
        public const long TextFileLimit = 2L * 1024 * 1024;
        public const int TextHeadBytes = 64 * 1024;
        /// <summary>A bigger binary file (crash dump) is skipped, so a report stays small enough to upload.</summary>
        public const long BinaryFileLimit = 16L * 1024 * 1024;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly List<(string Name, List<(string Key, string Value)> Fields)> _sections =
            new List<(string, List<(string, string)>)>();
        private readonly List<Breadcrumb> _breadcrumbs = new List<Breadcrumb>();
        private readonly List<ReportFileEntry> _files = new List<ReportFileEntry>();
        private readonly HashSet<string> _zipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ReportRedactor _redactor;

        /// <summary>Short id the player can quote, e.g. <c>R-7F3K2Q</c>.</summary>
        public string ReportId { get; }
        public DateTime CreatedUtc { get; }
        public string Description { get; }
        public string Category { get; }
        public string Contact { get; }

        /// <summary>Every planned file; sizes and notes are filled in by <see cref="WriteZip"/>.</summary>
        public IReadOnlyList<ReportFileEntry> Files => _files;

        internal List<Breadcrumb> BreadcrumbList => _breadcrumbs;

        internal ReportPackage(string reportId, DateTime createdUtc, ReportRequest request, ReportRedactor redactor)
        {
            ReportId = reportId;
            CreatedUtc = createdUtc;
            Description = request.Description ?? string.Empty;
            Category = request.Category ?? string.Empty;
            Contact = request.Contact ?? string.Empty;
            _redactor = redactor;
        }

        /// <summary>A section's fields as collected (not yet redacted), or an empty list.</summary>
        public IReadOnlyList<(string Key, string Value)> GetSection(string name)
        {
            foreach (var (sectionName, fields) in _sections)
                if (sectionName == name) return fields;
            return Array.Empty<(string, string)>();
        }

        internal ReportSectionBuilder BeginSection(string name)
        {
            var fields = new List<(string Key, string Value)>();
            _sections.Add((name, fields));
            return new ReportSectionBuilder(name, fields, this);
        }

        internal void AddFile(ReportFileEntry file)
        {
            if (string.IsNullOrEmpty(file.ZipPath) || !_zipPaths.Add(file.ZipPath))
            {
                Debug.LogWarning($"[Diagnostics] Report file '{file.ZipPath}' is empty or added twice; the copy is dropped.");
                return;
            }
            _files.Add(file);
        }

        /// <summary>Writes the zip to <paramref name="path"/> (replacing it). Any thread. The zip appears at the
        /// path only when it is complete, so an uploader never picks up half a file.</summary>
        public void WriteZip(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".part";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in _files) WriteEntry(zip, file);
                WriteBytes(zip, JsonName, Utf8.GetBytes(BuildJson()));
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        // ── Files ───────────────────────────────────────────────────────────────

        private void WriteEntry(ZipArchive zip, ReportFileEntry file)
        {
            try
            {
                byte[] bytes;
                switch (file.Kind)
                {
                    case ReportFileEntry.Source.TextFile:
                        if (!File.Exists(file.SourcePath)) { file.Note = "missing"; return; }
                        string text = ReportFileReader.ReadText(file.SourcePath, TextFileLimit, TextHeadBytes,
                                                                out long size, out bool trimmed);
                        file.SourceBytes = size;
                        if (trimmed) file.Note = "trimmed: the start and the end of a long log are kept";
                        bytes = Utf8.GetBytes(_redactor.Redact(text));
                        break;

                    case ReportFileEntry.Source.Text:
                        bytes = Utf8.GetBytes(_redactor.Redact(file.Text));
                        file.SourceBytes = bytes.Length;
                        break;

                    case ReportFileEntry.Source.BinaryFile:
                        if (!File.Exists(file.SourcePath)) { file.Note = "missing"; return; }
                        file.SourceBytes = new FileInfo(file.SourcePath).Length;
                        if (file.SourceBytes > BinaryFileLimit)
                        {
                            file.Note = string.Format(CultureInfo.InvariantCulture, "skipped: over the {0} MB limit",
                                                      BinaryFileLimit / (1024 * 1024));
                            return;
                        }
                        bytes = ReportFileReader.ReadBytes(file.SourcePath);
                        break;

                    default:   // Bytes
                        bytes = file.Bytes;
                        file.SourceBytes = bytes.Length;
                        break;
                }
                WriteBytes(zip, file.ZipPath, bytes);
                file.WrittenBytes = bytes.Length;
            }
            catch (Exception e)
            {
                file.Note = "unreadable: " + e.Message;
            }
        }

        private static void WriteBytes(ZipArchive zip, string zipPath, byte[] bytes)
        {
            var entry = zip.CreateEntry(zipPath, System.IO.Compression.CompressionLevel.Optimal);
            using (var stream = entry.Open()) stream.Write(bytes, 0, bytes.Length);
        }

        // ── report.json ─────────────────────────────────────────────────────────

        private string BuildJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var json = new StringBuilder(16 * 1024);
            json.Append("{\n");
            Property(json, 1, "format", Format, false).Append(",\n");
            Property(json, 1, "reportId", ReportId, false).Append(",\n");
            Property(json, 1, "createdUtc", CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", inv), false).Append(",\n");
            Property(json, 1, "description", Description, false).Append(",\n");   // the player's words: as typed
            Property(json, 1, "category", Category, false).Append(",\n");
            Property(json, 1, "contact", Contact, false).Append(",\n");

            json.Append("  \"sections\": {");
            for (int s = 0; s < _sections.Count; s++)
            {
                var (name, fields) = _sections[s];
                json.Append(s == 0 ? "\n" : ",\n");
                Indent(json, 2).Append(Quote(name)).Append(": {");
                for (int f = 0; f < fields.Count; f++)
                {
                    json.Append(f == 0 ? "\n" : ",\n");
                    Property(json, 3, fields[f].Key, fields[f].Value, true);
                }
                json.Append(fields.Count > 0 ? "\n    }" : "}");
            }
            json.Append(_sections.Count > 0 ? "\n  },\n" : "},\n");

            json.Append("  \"breadcrumbs\": [");
            for (int i = 0; i < _breadcrumbs.Count; i++)
            {
                var crumb = _breadcrumbs[i];
                json.Append(i == 0 ? "\n" : ",\n");
                Indent(json, 2).Append("{ \"time\": ").Append(crumb.Time.ToString("0.000", inv))
                    .Append(", \"frame\": ").Append(crumb.Frame.ToString(inv))
                    .Append(", \"category\": ").Append(Quote(_redactor.Redact(crumb.Category)))
                    .Append(", \"text\": ").Append(Quote(_redactor.Redact(crumb.Text)))
                    .Append(", \"scene\": ").Append(Quote(_redactor.Redact(crumb.Scene))).Append(" }");
            }
            json.Append(_breadcrumbs.Count > 0 ? "\n  ],\n" : "],\n");

            json.Append("  \"files\": [");
            for (int i = 0; i < _files.Count; i++)
            {
                var file = _files[i];
                json.Append(i == 0 ? "\n" : ",\n");
                Indent(json, 2).Append("{ \"path\": ").Append(Quote(file.ZipPath))
                    .Append(", \"sourceBytes\": ").Append(file.SourceBytes.ToString(inv))
                    .Append(", \"writtenBytes\": ").Append(file.WrittenBytes.ToString(inv));
                if (file.Note != null) json.Append(", \"note\": ").Append(Quote(_redactor.Redact(file.Note)));
                json.Append(" }");
            }
            json.Append(_files.Count > 0 ? "\n  ]\n" : "]\n");
            json.Append("}\n");
            return json.ToString();
        }

        private StringBuilder Property(StringBuilder json, int depth, string key, string value, bool redact) =>
            Indent(json, depth).Append(Quote(key)).Append(": ").Append(Quote(redact ? _redactor.Redact(value) : value));

        private static StringBuilder Indent(StringBuilder json, int depth) => json.Append(' ', depth * 2);

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':  quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < 0x20) quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else quoted.Append(c);
                        break;
                }
            }
            return quoted.Append('"').ToString();
        }
    }
}
