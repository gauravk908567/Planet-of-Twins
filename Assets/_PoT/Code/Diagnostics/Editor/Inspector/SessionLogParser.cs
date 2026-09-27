using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PoT.Diagnostics.Editor
{
    /// <summary>One entry of a session log: its first line plus the indented lines under it.</summary>
    public sealed class LogEntry
    {
        public const string Info = "INFO", Crumb = "CRUMB", Note = "NOTE", Warning = "WARNING", Error = "ERROR",
                            Assert = "ASSERT", Exception = "EXCEPTION";
        /// <summary>A line the parser couldn't read, or a "=== … removed here ===" marker.</summary>
        public const string Text = "TEXT", Cut = "CUT";

        /// <summary>Seconds since the session started.</summary>
        public double Time;
        /// <summary>The frame number, or "-" when the line was written off the main thread.</summary>
        public string Frame;
        public string Level;
        /// <summary>The channel or breadcrumb category; null for Unity warnings and errors.</summary>
        public string Tag;
        /// <summary>The first line of the message.</summary>
        public string Message;
        /// <summary>The indented lines (the rest of the message, the stack, notes), without the indent.</summary>
        public readonly List<string> Details = new List<string>();
        /// <summary>The entry's line in the file (1-based).</summary>
        public int Line;
        /// <summary>"(previous line repeated N more times)".</summary>
        public int Repeats;
        /// <summary>"(N copies of this line … were held back)", the flood limit.</summary>
        public int HeldBack;

        /// <summary>How many times this really happened: this line, its repeats and the copies held back.</summary>
        public int Occurrences => 1 + Repeats + HeldBack;

        public bool IsProblem => Severity > 0;

        /// <summary>2 = error / assert / exception, 1 = warning, 0 = anything else.</summary>
        public int Severity =>
            Level == Error || Level == Assert || Level == Exception ? 2 : Level == Warning ? 1 : 0;
    }

    /// <summary>A parsed session log (the format <c>SessionLog</c> writes, game.md §27.9).</summary>
    public sealed class SessionLogFile
    {
        /// <summary>The file's path in the report, e.g. <c>logs/session_20260927_101500.log</c>.</summary>
        public string Name;
        /// <summary>The <c>key: value</c> lines between <c>=== Session log ===</c> and <c>===</c>.</summary>
        public readonly List<(string Key, string Value)> Header = new List<(string, string)>();
        public readonly List<LogEntry> Entries = new List<LogEntry>();
        /// <summary>How many "=== … removed here … ===" markers the log has (the size limit trimmed it).</summary>
        public int Cuts;
        /// <summary>The end line without its <c>===</c>, e.g. "Session end (clean quit) at 812.4s"; null when the
        /// run crashed, was killed, or was still running when the report was made.</summary>
        public string End;

        public string HeaderValue(string key)
        {
            foreach (var (k, v) in Header)
                if (k == key) return v;
            return string.Empty;
        }
    }

    /// <summary>Reads session logs. Knows the line format only; nothing here is game-specific.</summary>
    public static class SessionLogParser
    {
        public const string FirstLine = "=== Session log ===";
        private const string HeaderEnd = "===";
        private const string EndPrefix = "=== Session end";

        // <time 000000.000> f<frame> <LEVEL> [<tag>] <message>; the level is padded, the tag is optional.
        private static readonly Regex LinePattern =
            new Regex(@"^(\d+\.\d+) f(\S+) +([A-Z]+) +(?:\[([^\]]*)\] ?)?(.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex RepeatPattern =
            new Regex(@"^\(previous line repeated (\d+) more times?\)", RegexOptions.CultureInvariant);
        private static readonly Regex HeldPattern =
            new Regex(@"^\(([\d,]+) cop(?:y|ies) of this line", RegexOptions.CultureInvariant);

        /// <summary>Whether <paramref name="text"/> is a session log (by its first line).</summary>
        public static bool IsSessionLog(string text) =>
            text != null && text.TrimStart('﻿').StartsWith(FirstLine, StringComparison.Ordinal);

        public static SessionLogFile Parse(string name, string text)
        {
            var log = new SessionLogFile { Name = name };
            if (text == null) return log;
            string[] lines = text.TrimStart('﻿').Split('\n');
            bool inHeader = false;
            LogEntry last = null;

            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].TrimEnd('\r');
                if (n == 0 && line == FirstLine) { inHeader = true; continue; }
                if (inHeader)
                {
                    if (line == HeaderEnd) { inHeader = false; continue; }
                    int colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (colon > 0) log.Header.Add((line.Substring(0, colon), line.Substring(colon + 2)));
                    continue;
                }
                if (line.Length == 0) continue;

                if (line[0] == ' ' || line[0] == '\t')
                {
                    string detail = line.StartsWith("    ", StringComparison.Ordinal) ? line.Substring(4) : line.TrimStart();
                    if (last == null) { last = Add(log, LogEntry.Text, detail, n); continue; }
                    last.Details.Add(detail);
                    CountNotes(last, detail);
                    continue;
                }

                if (line.StartsWith(EndPrefix, StringComparison.Ordinal))
                {
                    log.End = line.Trim('=', ' ');
                    last = null;
                    continue;
                }
                if (line.StartsWith("===", StringComparison.Ordinal))
                {
                    log.Cuts++;
                    last = Add(log, LogEntry.Cut, line.Trim('=', ' '), n);
                    continue;
                }

                var match = LinePattern.Match(line);
                if (!match.Success) { last = Add(log, LogEntry.Text, line, n); continue; }
                last = new LogEntry
                {
                    Time = double.Parse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                    Frame = match.Groups[2].Value,
                    Level = match.Groups[3].Value,
                    Tag = match.Groups[4].Success ? match.Groups[4].Value : null,
                    Message = match.Groups[5].Value,
                    Line = n + 1,
                };
                log.Entries.Add(last);
            }
            return log;
        }

        private static LogEntry Add(SessionLogFile log, string level, string text, int index)
        {
            double time = log.Entries.Count > 0 ? log.Entries[log.Entries.Count - 1].Time : 0;
            var entry = new LogEntry { Time = time, Frame = "-", Level = level, Message = text, Line = index + 1 };
            log.Entries.Add(entry);
            return entry;
        }

        private static void CountNotes(LogEntry entry, string detail)
        {
            var repeat = RepeatPattern.Match(detail);
            if (repeat.Success) { entry.Repeats += int.Parse(repeat.Groups[1].Value, CultureInfo.InvariantCulture); return; }
            var held = HeldPattern.Match(detail);
            if (held.Success)
                entry.HeldBack += int.Parse(held.Groups[1].Value.Replace(",", string.Empty), CultureInfo.InvariantCulture);
        }
    }
}
