using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Builds a bug report (game.md §27.3). Two steps, so the slow part can leave the main thread:
    ///   1. <see cref="Collect"/> (main thread): the package's sections (app, system, logs, crash), the breadcrumb
    ///      trail, the screenshot, then the game's sections from the request. Files are only planned.
    ///   2. <see cref="ReportPackage.WriteZip"/> (any thread): reads, trims and redacts the files and zips them.
    /// <see cref="WriteZipAsync"/> runs step 2 on a worker thread (the report screen uses it); <see cref="BuildZip"/>
    /// does both on the calling thread (the editor test menu uses it).
    ///
    /// Zips go to <c>persistentDataPath/Reports/</c>; the newest <see cref="KeepReports"/> are kept.
    /// </summary>
    public static class ReportCollector
    {
        public const string ReportsFolderName = "Reports";
        public const string FilePrefix = "report_";
        public const int KeepReports = 10;
        public const string CrumbCategory = "Report";

        public static string ReportsFolder => Path.Combine(Application.persistentDataPath, ReportsFolderName);

        /// <summary>Main thread. A section that throws is recorded in its own fields and logged as a warning;
        /// it never stops the report.</summary>
        public static ReportPackage Collect(ReportRequest request)
        {
            if (request == null) request = new ReportRequest();
            string logsFolder = Path.Combine(Application.persistentDataPath, DiagnosticsRuntime.LogsFolderName);

            var package = new ReportPackage(ReportId.New(), DateTime.UtcNow, request, ReportRedactor.ForThisMachine());
            Breadcrumbs.Add(CrumbCategory, $"report {package.ReportId} collected");
            Breadcrumbs.CopyTo(package.BreadcrumbList);

            Run(package, new AppReportSection());
            Run(package, new SystemReportSection());
            Run(package, new LogsReportSection(logsFolder));
            Run(package, new CrashReportSection(logsFolder, request.IncludePendingCrash));
            if (request.Screenshot != null && request.Screenshot.Length > 0)
                package.AddFile(ReportFileEntry.FromBytes(ReportPackage.ScreenshotName, request.Screenshot));

            if (request.Sections != null)
                foreach (var section in request.Sections) Run(package, section);
            return package;
        }

        /// <summary>Collects and writes the zip on the calling thread, then prunes old reports. Returns the zip path.</summary>
        public static string BuildZip(ReportRequest request, out ReportPackage package)
        {
            package = Collect(request);
            string path = ZipPathFor(package);
            package.WriteZip(path);
            PruneReports();
            return path;
        }

        /// <summary>Main thread: starts writing a collected package's zip on a worker thread, then prunes old
        /// reports there too. The task's result is the zip path; a failed write faults the task.</summary>
        public static Task<string> WriteZipAsync(ReportPackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            string path = ZipPathFor(package);   // Application paths are read here, on the main thread
            string folder = ReportsFolder;
            return Task.Run(() =>
            {
                package.WriteZip(path);
                PruneReports(folder, KeepReports);
                return path;
            });
        }

        /// <summary><c>Reports/report_&lt;local time&gt;_&lt;id&gt;.zip</c>: sorts by time, and the id is in the name.</summary>
        public static string ZipPathFor(ReportPackage package) =>
            Path.Combine(ReportsFolder, string.Format(CultureInfo.InvariantCulture, "{0}{1:yyyyMMdd_HHmmss}_{2}.zip",
                                                      FilePrefix, package.CreatedUtc.ToLocalTime(), package.ReportId));

        /// <summary>Deletes all but the newest <paramref name="keep"/> report zips.</summary>
        public static void PruneReports(int keep = KeepReports) => PruneReports(ReportsFolder, keep);

        // Any thread (no Unity API).
        private static void PruneReports(string folder, int keep)
        {
            if (!Directory.Exists(folder)) return;
            var files = Directory.GetFiles(folder, FilePrefix + "*.zip");
            Array.Sort(files, StringComparer.Ordinal);   // oldest first (the name starts with the time)
            for (int i = 0; i < files.Length - keep; i++)
            {
                try { File.Delete(files[i]); }
                catch (Exception) { /* open in another program: try again next time */ }
            }
        }

        private static void Run(ReportPackage package, IReportSection section)
        {
            if (section == null) return;
            var builder = package.BeginSection(section.Name);
            try
            {
                section.Collect(builder);
            }
            catch (Exception e)
            {
                builder.Add("Section error", $"{e.GetType().Name}: {e.Message}");
                Debug.LogWarning($"[Diagnostics] Report section '{section.Name}' failed; the report goes on without it.\n{e}");
            }
        }
    }
}
