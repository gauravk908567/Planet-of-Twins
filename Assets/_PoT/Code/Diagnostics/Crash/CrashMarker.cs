using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Detects that the previous run crashed or was killed, and keeps its evidence for a bug report.
    ///
    /// A <c>session.lock</c> file is written at start and deleted on a clean quit. If the lock is still there at
    /// launch, the previous run never quit cleanly, so its session log, Unity's <c>Player-prev.log</c> and the
    /// newest folder in Unity's crash-report folder (<c>crash.dmp</c>, <c>error.log</c>) are copied into
    /// <c>Logs/PendingCrash/</c>. A newer crash replaces an older one. The pending crash stays until the report is
    /// sent or the player dismisses it (<see cref="ClearPendingCrash"/>).
    ///
    /// Builds only: stopping Play in the Editor is not a crash, so the Editor never writes or reads the lock. It
    /// still reports a PendingCrash folder a build left behind (Editor and builds share persistentDataPath).
    /// </summary>
    public static class CrashMarker
    {
        public const string LockFileName = "session.lock";
        public const string PendingFolderName = "PendingCrash";
        public const string PendingSessionLogName = "session.log";
        public const string PendingPlayerLogName = "Player-prev.log";
        public const string PendingDumpFolderName = "crash";
        public const string PendingInfoName = "crash_info.txt";

        private static string _lockPath;

        /// <summary>The folder holding the last crash's evidence, or null when there is none.</summary>
        public static string PendingCrashFolder { get; private set; }

        public static bool HasPendingCrash => PendingCrashFolder != null;

        /// <summary>Checks for a crashed previous run, then arms the lock for this run. Returns true when a crash
        /// was detected just now. Call before this run's session log is created.</summary>
        internal static bool Arm(string logsFolder, string previousSessionLog)
        {
            bool crashed = false;
            if (!Application.isEditor)
            {
                _lockPath = Path.Combine(logsFolder, LockFileName);
                try
                {
                    Directory.CreateDirectory(logsFolder);
                    if (File.Exists(_lockPath))
                    {
                        CapturePending(logsFolder, File.GetLastWriteTimeUtc(_lockPath), previousSessionLog);
                        crashed = true;
                    }
                    File.WriteAllText(_lockPath, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                }
                catch (Exception e)
                {
                    _lockPath = null;
                    Debug.LogWarning($"[Diagnostics] Crash marker unavailable: {e.Message}");
                }
            }

            string pending = Path.Combine(logsFolder, PendingFolderName);
            PendingCrashFolder = Directory.Exists(pending) ? pending : null;
            return crashed;
        }

        /// <summary>Clean quit: this run did not crash.</summary>
        internal static void Disarm()
        {
            if (_lockPath == null) return;
            try { File.Delete(_lockPath); }
            catch (Exception) { /* the next launch will report a false crash; nothing better to do while quitting */ }
            _lockPath = null;
        }

        /// <summary>The crash was reported or dismissed: delete its evidence and stop flagging it.</summary>
        public static void ClearPendingCrash()
        {
            if (PendingCrashFolder == null) return;
            try { Directory.Delete(PendingCrashFolder, true); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Diagnostics] Could not clear the pending crash report: {e.Message}");
                return;
            }
            PendingCrashFolder = null;
        }

        internal static void ResetStatics()
        {
            _lockPath = null;
            PendingCrashFolder = null;
        }

        private static void CapturePending(string logsFolder, DateTime crashedRunStartUtc, string previousSessionLog)
        {
            string pending = Path.Combine(logsFolder, PendingFolderName);
            if (Directory.Exists(pending)) Directory.Delete(pending, true);   // keep only the latest crash
            Directory.CreateDirectory(pending);

            CopyIfExists(previousSessionLog, Path.Combine(pending, PendingSessionLogName));

            // Unity renamed the crashed run's Player.log to Player-prev.log when this run started.
            string playerLogFolder = Path.GetDirectoryName(Application.consoleLogPath);
            if (!string.IsNullOrEmpty(playerLogFolder))
                CopyIfExists(Path.Combine(playerLogFolder, "Player-prev.log"), Path.Combine(pending, PendingPlayerLogName));

            string dump = NewestCrashReportSince(crashedRunStartUtc);
            if (dump != null) CopyFiles(dump, Path.Combine(pending, PendingDumpFolderName));

            File.WriteAllText(Path.Combine(pending, PendingInfoName), string.Format(CultureInfo.InvariantCulture,
                "Detected at launch (UTC): {0:o}\nCrashed run started (UTC): {1:o}\nUnity crash report: {2}\n",
                DateTime.UtcNow, crashedRunStartUtc, dump != null ? Path.GetFileName(dump) : "none (killed, or a hang)"));
        }

        /// <summary>The newest folder Unity's crash handler created after <paramref name="sinceUtc"/>, or null.</summary>
        private static string NewestCrashReportSince(DateTime sinceUtc)
        {
#if UNITY_STANDALONE_WIN
            string root = UnityEngine.Windows.CrashReporting.crashReportFolder;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;

            string newest = null;
            DateTime newestTime = DateTime.MinValue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var created = Directory.GetCreationTimeUtc(dir);
                if (created >= sinceUtc && created > newestTime)
                {
                    newest = dir;
                    newestTime = created;
                }
            }
            return newest;
#else
            return null;
#endif
        }

        private static void CopyIfExists(string from, string to)
        {
            if (!string.IsNullOrEmpty(from) && File.Exists(from)) File.Copy(from, to, true);
        }

        private static void CopyFiles(string fromFolder, string toFolder)
        {
            Directory.CreateDirectory(toFolder);
            foreach (var file in Directory.GetFiles(fromFolder))
                File.Copy(file, Path.Combine(toFolder, Path.GetFileName(file)), true);
        }
    }
}
