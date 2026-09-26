using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// One text log per run in <c>persistentDataPath/Logs/</c>; the newest <see cref="KeepSessions"/> are kept.
    /// Header = app + system info. Lines = every Warning / Error / Assert / Exception Unity logs (through
    /// <c>Application.logMessageReceivedThreaded</c>), the enabled channels' Info lines, and breadcrumbs.
    /// Plain <c>Debug.Log</c> calls are NOT copied: channel lines would be doubled, and the rest is in Player.log.
    /// Every line is flushed at once, so the tail survives a hard crash. Thread-safe.
    ///
    /// Size (BUG-139): the file never passes <see cref="MaxBytes"/>. It always keeps its first <see cref="HeadBytes"/>
    /// (header, startup) and its newest <see cref="TailBytes"/>; the lines in between are removed and one marker line
    /// says how much (<see cref="BoundedLogFile"/>).
    ///
    /// Flood guards: an identical line repeated back-to-back is written once plus a "repeated N more times" note, and
    /// each different warning/error is written at most <see cref="FloodBurst"/> times per
    /// <see cref="FloodWindowSeconds"/> (numbers don't make a message different). Every warning/error is still
    /// counted in the problem ledger (<see cref="DescribeProblems"/>), which a report attaches and a clean quit writes
    /// at the end of the log.
    /// </summary>
    public static class SessionLog
    {
        public const int KeepSessions = 5;
        public const long MaxBytes = 16L * 1024 * 1024;
        public const long HeadBytes = 1L * 1024 * 1024;
        public const long TailBytes = 4L * 1024 * 1024;
        public const int FloodBurst = 20;
        public const double FloodWindowSeconds = 10;
        public const int MaxTrackedMessages = 1024;
        /// <summary>How many ledger entries a clean quit writes into the log (a report attaches them all).</summary>
        public const int ProblemsAtSessionEnd = 50;
        public const string FilePrefix = "session_";
        public const string FileExtension = ".log";

        private const string Indent = "    ";

        private static readonly object Gate = new object();
        private static BoundedLogFile _file;
        private static LogMessageTracker _tracker = NewTracker();
        private static string _lastKey;
        private static int _repeats;

        /// <summary>This run's log file, or null when it could not be opened.</summary>
        public static string CurrentPath { get; private set; }

        /// <summary>The newest session log in <paramref name="folder"/>, or null when there is none.</summary>
        public static string FindNewest(string folder)
        {
            var files = ListSessionLogs(folder);
            return files.Length > 0 ? files[files.Length - 1] : null;
        }

        /// <summary>This run's problem ledger (with Play stopped: the last run's) as text. Any thread.</summary>
        public static string DescribeProblems()
        {
            lock (Gate) return string.Join("\n", _tracker.Describe(MaxTrackedMessages)) + "\n";
        }

        /// <summary>One line on how much of this run's log was trimmed or held back, for a report. Any thread.</summary>
        public static string DescribeLimits()
        {
            lock (Gate)
            {
                var inv = CultureInfo.InvariantCulture;
                string trims = _file == null ? "log closed"
                    : _file.Trims == 0 ? "nothing trimmed"
                    : string.Format(inv, "{0:N0} KB of older lines trimmed in {1} trim{2}", _file.RemovedBytes / 1024,
                                    _file.Trims, _file.Trims == 1 ? "" : "s");
                if (_file != null && _file.Stopped) trims += " (stopped: " + _file.StopReason + ")";
                return string.Format(inv, "{0}; {1:N0} warning/error lines held back by the flood limit", trims,
                                     _tracker.HeldBackLines);
            }
        }

        // ── Lifecycle (DiagnosticsRuntime) ──────────────────────────────────────

        internal static void Open(string folder, IReadOnlyList<(string Key, string Value)> header)
        {
            string failure = null;
            lock (Gate)
            {
                if (_file != null) return;
                try
                {
                    Directory.CreateDirectory(folder);
                    DeleteOldest(folder, KeepSessions - 1);   // leave room for this session's file
                    string path = UniquePath(folder);
                    _file = new BoundedLogFile(path, MaxBytes, HeadBytes, TailBytes);
                    CurrentPath = path;
                    _tracker = NewTracker();
                    _lastKey = null;
                    _repeats = 0;

                    WriteRaw("=== Session log ===");
                    foreach (var (key, value) in header) WriteRaw($"{key}: {value}");
                    WriteRaw("===");
                    _file.EndEntry();
                }
                catch (Exception e)
                {
                    DisposeFile();
                    failure = e.Message;
                }
            }
            // Outside the lock, and the file is null, so the log hook ignores this line.
            if (failure != null)
                Debug.LogWarning($"[Diagnostics] Session log could not be opened in '{folder}': {failure}");
        }

        internal static void Close(string reason)
        {
            lock (Gate)
            {
                if (_file == null) return;
                try
                {
                    FlushRepeats();
                    if (_tracker.TotalLines > 0)
                    {
                        var problems = _tracker.Describe(ProblemsAtSessionEnd);
                        WriteRaw(FormatLine(DiagnosticsClock.Elapsed, -1, "NOTE", null, problems[0]));
                        for (int i = 1; i < problems.Count; i++) WriteRaw(Indent + problems[i]);
                        _file.EndEntry();
                    }
                    WriteRaw(string.Format(CultureInfo.InvariantCulture, "=== Session end ({0}) at {1:0.000}s ===",
                                           reason, DiagnosticsClock.Elapsed));
                }
                catch (Exception) { /* closing anyway */ }
                DisposeFile();
            }
        }

        // ── Writers ─────────────────────────────────────────────────────────────

        internal static void WriteChannel(string channel, string message) =>
            Write(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, "INFO", channel, message, null, false);

        internal static void WriteCrumb(in Breadcrumb crumb) =>
            Write(crumb.Time, crumb.Frame, "CRUMB", crumb.Category,
                  string.IsNullOrEmpty(crumb.Scene) ? crumb.Text : $"{crumb.Text}  @{crumb.Scene}", null, false);

        /// <summary>A line about the diagnostics system itself (e.g. "previous session crashed").</summary>
        internal static void WriteNote(string message) =>
            Write(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, "NOTE", null, message, null, false);

        /// <summary>Handler for <c>Application.logMessageReceivedThreaded</c>. Any thread.</summary>
        internal static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            string level;
            switch (type)
            {
                case LogType.Warning:   level = "WARNING"; break;
                case LogType.Error:     level = "ERROR"; break;
                case LogType.Assert:    level = "ASSERT"; break;
                case LogType.Exception: level = "EXCEPTION"; break;
                default: return;   // LogType.Log (see the class summary)
            }
            Write(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, level, null, condition, stackTrace, true);
        }

        /// <summary>Runs <paramref name="read"/> while this run's log can't change, when <paramref name="path"/> is
        /// this run's log (a trim rewrites its end); any other file is read directly. <paramref name="read"/> must
        /// not log. Any thread.</summary>
        internal static T ReadStable<T>(string path, Func<T> read)
        {
            if (!IsCurrent(path)) return read();
            lock (Gate) return read();
        }

        private static void Write(double time, int frame, string level, string tag, string message, string stack,
                                  bool track)
        {
            lock (Gate)
            {
                if (_file == null || _file.Stopped) return;
                try
                {
                    string key = level + "|" + tag + "|" + message;
                    if (key == _lastKey)
                    {
                        _repeats++;
                        if (track) _tracker.Count(level, message, time);
                        return;
                    }
                    FlushRepeats();

                    int heldBefore = 0;
                    double heldSeconds = 0;
                    if (track && !_tracker.Allow(level, message, stack, time, out heldBefore, out heldSeconds))
                    {
                        _lastKey = null;   // held back: the next copy of the last written line is not a back-to-back repeat
                        return;
                    }
                    _lastKey = key;

                    WriteRaw(FormatLine(time, frame, level, tag, message));
                    if (!string.IsNullOrEmpty(stack)) WriteStack(stack);
                    if (heldBefore > 0)
                        WriteRaw(string.Format(CultureInfo.InvariantCulture,
                            "{0}({1:N0} copies of this line in the {2:0.#} s before it were held back: the log keeps {3} " +
                            "per {4:0.#} s of each message)", Indent, heldBefore, heldSeconds, FloodBurst, FloodWindowSeconds));
                    _file.EndEntry();
                }
                // Never log from here: it would re-enter this hook. A dead file just stops the session log.
                catch (Exception) { DisposeFile(); }
            }
        }

        // ── Helpers (call inside the lock) ──────────────────────────────────────

        private static string FormatLine(double time, int frame, string level, string tag, string message)
        {
            string f = frame >= 0 ? frame.ToString(CultureInfo.InvariantCulture) : "-";
            if (message == null) message = string.Empty;
            if (message.IndexOf('\n') >= 0)
                message = message.Replace("\r\n", "\n").TrimEnd('\n').Replace("\n", "\n" + Indent);
            // Zero-padded time: a line never starts with a space, so an indented line is always a continuation.
            return tag == null
                ? string.Format(CultureInfo.InvariantCulture, "{0:000000.000} f{1,-7} {2,-9} {3}", time, f, level, message)
                : string.Format(CultureInfo.InvariantCulture, "{0:000000.000} f{1,-7} {2,-9} [{3}] {4}",
                                time, f, level, tag, message);
        }

        private static void WriteStack(string stack)
        {
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length > 0) WriteRaw(Indent + line);
            }
        }

        // The note continues the last written line, so it goes out before anything else is written.
        private static void FlushRepeats()
        {
            if (_repeats == 0) return;
            WriteRaw($"{Indent}(previous line repeated {_repeats} more times)");
            _repeats = 0;
            _file.EndEntry();
        }

        private static void WriteRaw(string line) => _file.WriteLine(line);

        private static void DisposeFile()
        {
            _file?.Dispose();
            _file = null;
        }

        private static bool IsCurrent(string path)
        {
            string current = CurrentPath;
            if (current == null || string.IsNullOrEmpty(path)) return false;
            try { return string.Equals(Path.GetFullPath(path), Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
        }

        private static LogMessageTracker NewTracker() =>
            new LogMessageTracker(FloodBurst, FloodWindowSeconds, MaxTrackedMessages);

        /// <summary>The session logs in <paramref name="folder"/>, oldest first.</summary>
        internal static string[] ListSessionLogs(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();
            var files = Directory.GetFiles(folder, FilePrefix + "*" + FileExtension);
            Array.Sort(files, StringComparer.Ordinal);   // names carry a sortable timestamp → oldest first
            return files;
        }

        private static void DeleteOldest(string folder, int keep)
        {
            var files = ListSessionLogs(folder);
            for (int i = 0; i < files.Length - keep; i++)
            {
                try { File.Delete(files[i]); }
                catch (Exception) { /* in use by another running copy: try again next launch */ }
            }
        }

        private static string UniquePath(string folder)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(folder, FilePrefix + stamp + FileExtension);
            for (int n = 2; File.Exists(path); n++)
                path = Path.Combine(folder, $"{FilePrefix}{stamp}_{n}{FileExtension}");
            return path;
        }

        internal static void ResetStatics()
        {
            lock (Gate)
            {
                DisposeFile();
                CurrentPath = null;
                _tracker = NewTracker();
                _lastKey = null;
                _repeats = 0;
            }
        }
    }
}
