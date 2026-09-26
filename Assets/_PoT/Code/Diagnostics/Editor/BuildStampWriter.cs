using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace PoT.Diagnostics.Editor
{
    /// <summary>
    /// After a successful player build, writes <see cref="BuildStamp.FileName"/> into the build's data folder:
    /// the git commit, branch, whether tracked files had local changes, and the build time. Reports and session
    /// logs then name the exact commit a player ran. Without git on PATH the stamp says "unknown"; the build is
    /// never failed over it. Standalone Windows, Linux and macOS.
    /// </summary>
    public sealed class BuildStampWriter : IPostprocessBuildWithReport
    {
        private const int GitTimeoutMs = 15000;

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            string dataFolder = DataFolderFor(report.summary.platform, report.summary.outputPath);
            if (dataFolder == null || !Directory.Exists(dataFolder))
            {
                Debug.LogWarning($"[Diagnostics] No build stamp written: no data folder found for {report.summary.platform} " +
                                 $"at '{report.summary.outputPath}'. Reports from this build will say 'no build stamp'.");
                return;
            }

            string status = Git("status --porcelain --untracked-files=no");
            var lines = new List<string>
            {
                "Commit: " + (Git("rev-parse --short=10 HEAD") ?? "unknown (git not available)"),
                "Branch: " + (Git("rev-parse --abbrev-ref HEAD") ?? "unknown"),
                "Local changes: " + (status == null ? "unknown" : status.Length > 0 ? "yes (tracked files differ from the commit)" : "no"),
                "Built (UTC): " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            };

            try
            {
                File.WriteAllLines(Path.Combine(dataFolder, BuildStamp.FileName), lines, new UTF8Encoding(false));
                Debug.Log($"[Diagnostics] Build stamp: {string.Join(" · ", lines)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Diagnostics] Build stamp could not be written: {e.Message}");
            }
        }

        // Application.dataPath in the player: <Game>_Data next to the exe (Windows, Linux), <Game>.app/Contents (macOS).
        private static string DataFolderFor(BuildTarget platform, string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return null;
            switch (platform)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty,
                                        Path.GetFileNameWithoutExtension(outputPath) + "_Data");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(outputPath, "Contents");
                default:
                    return null;
            }
        }

        /// <summary>Runs git in the project folder; its trimmed output, or null when git fails or times out.</summary>
        private static string Git(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),   // the project root in the Editor
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var process = Process.Start(start))
                {
                    if (process == null) return null;
                    var output = process.StandardOutput.ReadToEndAsync();
                    process.StandardError.ReadToEndAsync();   // drained so a chatty stderr can't block git
                    if (!process.WaitForExit(GitTimeoutMs))
                    {
                        try { process.Kill(); } catch (Exception) { /* already gone */ }
                        return null;
                    }
                    return process.ExitCode == 0 ? output.Result.Trim() : null;
                }
            }
            catch (Exception)
            {
                return null;   // git not installed / not on PATH
            }
        }
    }
}
