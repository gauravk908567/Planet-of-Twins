using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
    /// Flood guards: an identical line repeated back-to-back is written once plus a "repeated N more times" note,
    /// and the file stops growing after <see cref="MaxChars"/> characters.
    /// </summary>
    public static class SessionLog
    {
        public const int KeepSessions = 5;
        public const long MaxChars = 16L * 1024 * 1024;
        public const string FilePrefix = "session_";
        public const string FileExtension = ".log";

        private const string Indent = "    ";

        private static readonly object Gate = new object();
        private static StreamWriter _writer;
        private static long _chars;
        private static bool _capped;
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

        // ── Lifecycle (DiagnosticsRuntime) ──────────────────────────────────────

        internal static void Open(string folder, IReadOnlyList<(string Key, string Value)> header)
        {
            string failure = null;
            lock (Gate)
            {
                if (_writer != null) return;
                try
                {
                    Directory.CreateDirectory(folder);
                    DeleteOldest(folder, KeepSessions - 1);   // leave room for this session's file
                    string path = UniquePath(folder);
                    var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                                                FileShare.ReadWrite | FileShare.Delete);
                    _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    CurrentPath = path;
                    _chars = 0;
                    _capped = false;
                    _lastKey = null;
                    _repeats = 0;

                    WriteRaw("=== Session log ===");
                    foreach (var (key, value) in header) WriteRaw($"{key}: {value}");
                    WriteRaw("===");
                }
                catch (Exception e)
                {
                    DisposeWriter();
                    failure = e.Message;
                }
            }
            // Outside the lock, and the writer is null, so the log hook ignores this line.
            if (failure != null)
                Debug.LogWarning($"[Diagnostics] Session log could not be opened in '{folder}': {failure}");
        }

        internal static void Close(string reason)
        {
            lock (Gate)
            {
                if (_writer == null) return;
                try
                {
                    FlushRepeats();
                    WriteRaw(string.Format(CultureInfo.InvariantCulture, "=== Session end ({0}) at {1:0.000}s ===",
                                           reason, DiagnosticsClock.Elapsed));
                }
                catch (Exception) { /* closing anyway */ }
                DisposeWriter();
            }
        }

        // ── Writers ─────────────────────────────────────────────────────────────

        internal static void WriteChannel(string channel, string message) =>
            Write(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, "INFO", channel, message, null);

        internal static void WriteCrumb(in Breadcrumb crumb) =>
            Write(crumb.Time, crumb.Frame, "CRUMB", crumb.Category,
                  string.IsNullOrEmpty(crumb.Scene) ? crumb.Text : $"{crumb.Text}  @{crumb.Scene}", null);

        /// <summary>A line about the diagnostics system itself (e.g. "previous session crashed").</summary>
        internal static void WriteNote(string message) =>
            Write(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, "NOTE", null, message, null);

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
            Write(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, level, null, condition, stackTrace);
        }

        private static void Write(double time, int frame, string level, string tag, string message, string stack)
        {
            lock (Gate)
            {
                if (_writer == null || _capped) return;
                try
                {
                    string key = level + "|" + tag + "|" + message;
                    if (key == _lastKey) { _repeats++; return; }
                    FlushRepeats();
                    _lastKey = key;

                    WriteRaw(FormatLine(time, frame, level, tag, message));
                    if (!string.IsNullOrEmpty(stack)) WriteStack(stack);

                    if (_chars > MaxChars)
                    {
                        _capped = true;
                        WriteRaw($"=== Size cap reached ({MaxChars / (1024 * 1024)} MB of text): later lines dropped ===");
                    }
                }
                // Never log from here: it would re-enter this hook. A dead file just stops the session log.
                catch (Exception) { DisposeWriter(); }
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

        private static void FlushRepeats()
        {
            if (_repeats == 0) return;
            WriteRaw($"{Indent}(previous line repeated {_repeats} more times)");
            _repeats = 0;
        }

        private static void WriteRaw(string line)
        {
            _writer.WriteLine(line);
            _chars += line.Length + 1;
        }

        private static void DisposeWriter()
        {
            try { _writer?.Dispose(); }
            catch (Exception) { /* already broken */ }
            _writer = null;
        }

        private static string[] ListSessionLogs(string folder)
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
                DisposeWriter();
                CurrentPath = null;
                _chars = 0;
                _capped = false;
                _lastKey = null;
                _repeats = 0;
            }
        }
    }
}
