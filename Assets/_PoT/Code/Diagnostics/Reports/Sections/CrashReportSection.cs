using System;
using System.IO;

namespace PoT.Diagnostics
{
    /// <summary>
    /// <c>crash</c>: the evidence <see cref="CrashMarker"/> kept from the last run that crashed or was killed
    /// (<c>Logs/PendingCrash/</c>): its session log, Player-prev.log, <c>crash_info.txt</c> (also copied into this
    /// section's fields) and Unity's crash folder (<c>crash.dmp</c>, <c>error.log</c>). The folder is read from
    /// disk, so the Editor's test report sees a crash a build left behind.
    /// </summary>
    internal sealed class CrashReportSection : IReportSection
    {
        private const string ZipFolder = "crash/";
        private static readonly string[] TextExtensions = { ".log", ".txt", ".json", ".xml", ".csv" };

        private readonly string _logsFolder;
        private readonly bool _include;

        internal CrashReportSection(string logsFolder, bool include)
        {
            _logsFolder = logsFolder;
            _include = include;
        }

        public string Name => "crash";

        public void Collect(ReportSectionBuilder section)
        {
            string folder = Path.Combine(_logsFolder, CrashMarker.PendingFolderName);
            if (!Directory.Exists(folder)) { section.Add("Pending crash", "none"); return; }
            if (!_include) { section.Add("Pending crash", "yes, not included in this report"); return; }

            section.Add("Pending crash", "yes");
            string info = Path.Combine(folder, CrashMarker.PendingInfoName);
            if (File.Exists(info))
            {
                foreach (var line in File.ReadAllLines(info))
                {
                    int colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (colon > 0) section.Add(line.Substring(0, colon), line.Substring(colon + 2));
                }
            }

            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string zipPath = ZipFolder + file.Substring(folder.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (IsText(file)) section.AddTextFile(zipPath, file);
                else section.AddBinaryFile(zipPath, file);
            }
        }

        private static bool IsText(string path)
        {
            string extension = Path.GetExtension(path);
            foreach (var text in TextExtensions)
                if (string.Equals(extension, text, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
