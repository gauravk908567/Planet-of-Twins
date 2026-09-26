namespace PoT.Diagnostics
{
    /// <summary>
    /// One file planned for a report zip. It is only read when the zip is written, so the size and note are
    /// known after <see cref="ReportPackage.WriteZip"/>. Every entry is listed in report.json's <c>files</c>,
    /// including the ones that were missing or skipped, so a reader can tell "not there" from "not collected".
    /// </summary>
    public sealed class ReportFileEntry
    {
        internal enum Source { TextFile, Text, BinaryFile, Bytes }

        internal readonly Source Kind;
        internal readonly string SourcePath;
        internal readonly string Text;
        internal readonly byte[] Bytes;

        /// <summary>The path inside the zip, with forward slashes (e.g. <c>logs/Player.log</c>).</summary>
        public string ZipPath { get; }

        /// <summary>The source's size in bytes, or -1 when it could not be read.</summary>
        public long SourceBytes { get; internal set; } = -1;

        /// <summary>Bytes written into the zip (before compression); 0 when the file was not included.</summary>
        public long WrittenBytes { get; internal set; }

        /// <summary>Why the file differs from its source ("trimmed…", "missing", "skipped…"), or null.</summary>
        public string Note { get; internal set; }

        private ReportFileEntry(Source kind, string zipPath, string sourcePath, string text, byte[] bytes)
        {
            Kind = kind;
            ZipPath = zipPath;
            SourcePath = sourcePath;
            Text = text;
            Bytes = bytes;
        }

        internal static ReportFileEntry TextFile(string zipPath, string sourcePath) =>
            new ReportFileEntry(Source.TextFile, zipPath, sourcePath, null, null);

        internal static ReportFileEntry FromText(string zipPath, string text) =>
            new ReportFileEntry(Source.Text, zipPath, null, text ?? string.Empty, null);

        internal static ReportFileEntry BinaryFile(string zipPath, string sourcePath) =>
            new ReportFileEntry(Source.BinaryFile, zipPath, sourcePath, null, null);

        internal static ReportFileEntry FromBytes(string zipPath, byte[] bytes) =>
            new ReportFileEntry(Source.Bytes, zipPath, null, null, bytes ?? System.Array.Empty<byte>());
    }
}
