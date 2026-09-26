using System.Collections.Generic;

namespace PoT.Diagnostics
{
    /// <summary>
    /// What an <see cref="IReportSection"/> writes into: its fields in report.json and the files it adds to the
    /// zip. Files are only planned here. They are read, trimmed and redacted when the zip is written, which can
    /// run off the main thread. Everything a section adds is redacted (user name, profile path, machine name,
    /// email addresses); only the player's own description and contact are sent as typed.
    /// </summary>
    public sealed class ReportSectionBuilder
    {
        private readonly List<(string Key, string Value)> _fields;
        private readonly ReportPackage _package;

        internal ReportSectionBuilder(string name, List<(string Key, string Value)> fields, ReportPackage package)
        {
            Name = name;
            _fields = fields;
            _package = package;
        }

        /// <summary>This section's key under <c>sections</c> in report.json.</summary>
        public string Name { get; }

        /// <summary>A field of this section. Keys keep their order; null values are written as "".</summary>
        public void Add(string key, string value) => _fields.Add((key ?? string.Empty, value ?? string.Empty));

        public void AddRange(IEnumerable<(string Key, string Value)> fields)
        {
            foreach (var (key, value) in fields) Add(key, value);
        }

        /// <summary>A text file from disk (a log, a save). A file longer than
        /// <see cref="ReportPackage.TextFileLimit"/> keeps its start and its end (the cut is marked in the text).
        /// A missing file is listed as missing, not an error. The file may be open for writing elsewhere.</summary>
        public void AddTextFile(string zipPath, string sourcePath) =>
            _package.AddFile(ReportFileEntry.TextFile(zipPath, sourcePath));

        /// <summary>Text built in memory.</summary>
        public void AddText(string zipPath, string text) =>
            _package.AddFile(ReportFileEntry.FromText(zipPath, text));

        /// <summary>A binary file from disk (a crash dump), copied as it is. It is skipped (and listed as skipped)
        /// above <see cref="ReportPackage.BinaryFileLimit"/>. Binary data can't be redacted.</summary>
        public void AddBinaryFile(string zipPath, string sourcePath) =>
            _package.AddFile(ReportFileEntry.BinaryFile(zipPath, sourcePath));

        /// <summary>Bytes built in memory (a screenshot).</summary>
        public void AddBytes(string zipPath, byte[] bytes) =>
            _package.AddFile(ReportFileEntry.FromBytes(zipPath, bytes));
    }
}
