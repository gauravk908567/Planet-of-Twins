using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// <c>logs</c>: the two newest session logs (this run's and the one before; with Play stopped, the last two
    /// runs), and Unity's own log with the previous run's copy (Player.log / Player-prev.log in a build,
    /// Editor.log / Editor-prev.log in the Editor). Long logs are trimmed to their start and end.
    /// </summary>
    internal sealed class LogsReportSection : IReportSection
    {
        private const int SessionLogsIncluded = 2;
        private const string ZipFolder = "logs/";

        private readonly string _logsFolder;

        internal LogsReportSection(string logsFolder) => _logsFolder = logsFolder;

        public string Name => "logs";

        public void Collect(ReportSectionBuilder section)
        {
            var sessionLogs = SessionLog.ListSessionLogs(_logsFolder);   // oldest first
            var included = new List<string>();
            for (int i = sessionLogs.Length - 1; i >= 0 && included.Count < SessionLogsIncluded; i--)
            {
                string name = Path.GetFileName(sessionLogs[i]);
                section.AddTextFile(ZipFolder + name, sessionLogs[i]);
                included.Add(name);
            }
            section.Add("Session logs", included.Count > 0 ? string.Join(", ", included) : "none");

            string unityLog = Application.consoleLogPath;
            if (string.IsNullOrEmpty(unityLog))
            {
                section.Add("Unity log", "none (logging to a file is off)");
                return;
            }
            section.AddTextFile(ZipFolder + Path.GetFileName(unityLog), unityLog);
            section.Add("Unity log", Path.GetFileName(unityLog));

            string previous = Path.Combine(Path.GetDirectoryName(unityLog) ?? string.Empty,
                Path.GetFileNameWithoutExtension(unityLog) + "-prev" + Path.GetExtension(unityLog));
            if (File.Exists(previous))
            {
                section.AddTextFile(ZipFolder + Path.GetFileName(previous), previous);
                section.Add("Previous Unity log", Path.GetFileName(previous));
            }
        }
    }
}
