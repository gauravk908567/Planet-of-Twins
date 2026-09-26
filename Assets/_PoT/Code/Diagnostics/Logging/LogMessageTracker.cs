using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Keeps a ledger of every DIFFERENT warning/error of a session, and limits how often each one is written.
    ///
    /// "Different" ignores numbers: <c>dist 12.5 (Enemy 7)</c> and <c>dist 3.1 (Enemy 2)</c> are the same message, so a
    /// warning raised every frame counts as one problem.
    /// - Flood limit: at most <see cref="Burst"/> copies of a message per <see cref="WindowSeconds"/>. Copies over the
    ///   limit are held back and counted; the next copy that is written reports how many were held back before it.
    /// - Ledger: per message, the total count, the first and last time, and the first copy's text and stack, so
    ///   trimming or the flood limit never hides that a problem happened (<see cref="Describe"/>).
    /// At most <see cref="MaxTracked"/> different messages are tracked; messages after that are written as usual and
    /// only counted in <see cref="Untracked"/>. Not thread-safe: the owner locks.
    /// </summary>
    public sealed class LogMessageTracker
    {
        private const int KeyChars = 300;
        private const int SampleChars = 500;
        private const int SampleStackLines = 5;

        /// <summary>One different message and what happened to it this session.</summary>
        public sealed class Entry
        {
            public string Level { get; internal set; }
            public string Sample { get; internal set; }
            public string SampleStack { get; internal set; }
            public long Count { get; internal set; }
            public long HeldBack { get; internal set; }
            public double FirstTime { get; internal set; }
            public double LastTime { get; internal set; }

            internal double WindowStart;
            internal int WindowWritten;
            internal int WindowHeld;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly StringBuilder _key = new StringBuilder(KeyChars + 16);

        public int Burst { get; }
        public double WindowSeconds { get; }
        public int MaxTracked { get; }

        /// <summary>Lines of messages that arrived after <see cref="MaxTracked"/> were already tracked.</summary>
        public long Untracked { get; private set; }

        public long TotalLines { get; private set; }
        public long HeldBackLines { get; private set; }

        public IReadOnlyCollection<Entry> Entries => _entries.Values;

        public LogMessageTracker(int burst, double windowSeconds, int maxTracked)
        {
            Burst = Math.Max(1, burst);
            WindowSeconds = Math.Max(0.001, windowSeconds);
            MaxTracked = Math.Max(1, maxTracked);
        }

        /// <summary>Counts one copy and decides whether it is written. When it is, and copies were held back since
        /// this message was last written, <paramref name="heldBefore"/> says how many (over the last
        /// <paramref name="heldSeconds"/>) so the caller can note it after the line.</summary>
        public bool Allow(string level, string message, string stack, double now, out int heldBefore, out double heldSeconds)
        {
            heldBefore = 0;
            heldSeconds = 0;
            TotalLines++;
            var entry = Find(level, message, true, now, stack);
            if (entry == null) return true;   // untracked: never limited

            entry.Count++;
            entry.LastTime = now;
            if (now - entry.WindowStart >= WindowSeconds)
            {
                heldBefore = entry.WindowHeld;
                heldSeconds = now - entry.WindowStart;
                entry.WindowStart = now;
                entry.WindowWritten = 1;
                entry.WindowHeld = 0;
                return true;
            }
            if (entry.WindowWritten < Burst)
            {
                entry.WindowWritten++;
                return true;
            }
            entry.WindowHeld++;
            entry.HeldBack++;
            HeldBackLines++;
            return false;
        }

        /// <summary>Counts a copy that the caller collapsed some other way (a back-to-back repeat): no flood check.</summary>
        public void Count(string level, string message, double now)
        {
            TotalLines++;
            var entry = Find(level, message, false, now, null);
            if (entry == null) return;
            entry.Count++;
            entry.LastTime = now;
        }

        /// <summary>The ledger as text lines: a title line, then per message a line at no indent and its sample
        /// (message + first stack lines) indented 4 spaces. Most severe first, then most frequent. At most
        /// <paramref name="maxEntries"/> messages.</summary>
        public List<string> Describe(int maxEntries)
        {
            var inv = CultureInfo.InvariantCulture;
            var lines = new List<string>();
            var sorted = new List<Entry>(_entries.Values);
            sorted.Sort(CompareEntries);

            lines.Add(string.Format(inv,
                "Problems this session: {0} different warning/error message{1}, {2:N0} line{3}, {4:N0} held back by the " +
                "flood limit ({5} per {6:0.#} s of each message; numbers don't make a message different).",
                sorted.Count, sorted.Count == 1 ? "" : "s", TotalLines, TotalLines == 1 ? "" : "s", HeldBackLines,
                Burst, WindowSeconds));

            int shown = Math.Min(sorted.Count, Math.Max(0, maxEntries));
            for (int i = 0; i < shown; i++)
            {
                var e = sorted[i];
                string held = e.HeldBack > 0 ? string.Format(inv, ", {0:N0} held back", e.HeldBack) : string.Empty;
                lines.Add(string.Format(inv, "{0} ×{1:N0}{2}  first {3:000000.000}  last {4:000000.000}",
                                        e.Level, e.Count, held, e.FirstTime, e.LastTime));
                AddIndented(lines, e.Sample);
                AddIndented(lines, e.SampleStack);
            }
            if (sorted.Count > shown)
                lines.Add(string.Format(inv, "(and {0} more different messages, less severe or less frequent)", sorted.Count - shown));
            if (Untracked > 0)
                lines.Add(string.Format(inv, "(and {0:N0} lines of messages that arrived after {1:N0} different ones were " +
                                             "tracked: written to the log, not counted here)", Untracked, MaxTracked));
            return lines;
        }

        private Entry Find(string level, string message, bool create, double now, string stack)
        {
            string key = KeyOf(level, message);
            if (_entries.TryGetValue(key, out var entry)) return entry;
            if (!create || _entries.Count >= MaxTracked)
            {
                Untracked++;
                return null;
            }
            entry = new Entry
            {
                Level = level,
                Sample = Clip(message, SampleChars),
                SampleStack = FirstLines(stack, SampleStackLines),
                FirstTime = now,
                WindowStart = now,
            };
            _entries.Add(key, entry);
            return entry;
        }

        // Level + the message's start with every run of digits folded to '#'.
        private string KeyOf(string level, string message)
        {
            _key.Clear().Append(level).Append('|');
            if (message == null) return _key.ToString();
            int end = Math.Min(message.Length, KeyChars);
            bool inDigits = false;
            for (int i = 0; i < end; i++)
            {
                char c = message[i];
                if (c >= '0' && c <= '9')
                {
                    if (!inDigits) _key.Append('#');
                    inDigits = true;
                }
                else
                {
                    _key.Append(c);
                    inDigits = false;
                }
            }
            return _key.ToString();
        }

        private static int CompareEntries(Entry a, Entry b)
        {
            int bySeverity = Severity(b.Level).CompareTo(Severity(a.Level));
            if (bySeverity != 0) return bySeverity;
            int byCount = b.Count.CompareTo(a.Count);
            return byCount != 0 ? byCount : a.FirstTime.CompareTo(b.FirstTime);
        }

        private static int Severity(string level)
        {
            switch (level)
            {
                case "EXCEPTION": return 4;
                case "ERROR": return 3;
                case "ASSERT": return 2;
                case "WARNING": return 1;
                default: return 0;
            }
        }

        private static void AddIndented(List<string> lines, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length > 0) lines.Add("    " + line);
            }
        }

        private static string Clip(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + " …";
        }

        private static string FirstLines(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var result = new StringBuilder();
            int taken = 0;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (taken == count) { result.Append("…"); break; }
                result.Append(line).Append('\n');
                taken++;
            }
            return result.ToString().TrimEnd('\n');
        }
    }
}
