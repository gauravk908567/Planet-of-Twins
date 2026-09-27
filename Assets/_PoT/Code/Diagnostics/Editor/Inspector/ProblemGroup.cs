using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PoT.Diagnostics.Editor
{
    /// <summary>
    /// Every warning/error with the same signature: its level, its first message line with the numbers folded (so
    /// <c>dist 12.5</c> = <c>dist 3.1</c>), and the top 3 stack frames below Unity's logging calls. Used inside one
    /// report and across reports ("14 reports, all on this GPU").
    /// </summary>
    public sealed class ProblemGroup
    {
        private static readonly Regex Digits = new Regex(@"\d+", RegexOptions.CultureInvariant);
        private const int SignatureFrames = 3;

        public readonly string Signature;
        /// <summary>The first entry seen with this signature, and the log it came from.</summary>
        public readonly LogEntry Sample;
        public readonly string SampleLog;
        /// <summary>How many times it happened (repeats and held-back copies included).</summary>
        public int Occurrences;
        public double FirstTime, LastTime;
        /// <summary>The logs (and, across reports, the reports) it appears in.</summary>
        public readonly List<string> Logs = new List<string>();
        public readonly List<InspectedReport> Reports = new List<InspectedReport>();

        public ProblemGroup(string signature, LogEntry sample, string sampleLog)
        {
            Signature = signature;
            Sample = sample;
            SampleLog = sampleLog;
            FirstTime = LastTime = sample.Time;
        }

        public int Severity => Sample.Severity;

        public static string SignatureOf(LogEntry entry)
        {
            var signature = new StringBuilder();
            signature.Append(entry.Severity).Append('|').Append(Digits.Replace(entry.Message ?? string.Empty, "#"));
            int frames = 0;
            foreach (var line in entry.Details)
            {
                if (!StackFrames.TryParse(line, out var frame) || frame.IsLoggingCall) continue;
                signature.Append('|').Append(frame.Method);
                if (++frames == SignatureFrames) break;
            }
            return signature.ToString();
        }

        /// <summary>Groups the problems of <paramref name="logs"/>; most severe first, then most frequent.</summary>
        public static List<ProblemGroup> GroupAll(IEnumerable<SessionLogFile> logs)
        {
            var bySignature = new Dictionary<string, ProblemGroup>();
            var groups = new List<ProblemGroup>();
            foreach (var log in logs)
            {
                foreach (var entry in log.Entries)
                {
                    if (!entry.IsProblem) continue;
                    string signature = SignatureOf(entry);
                    if (!bySignature.TryGetValue(signature, out var group))
                    {
                        group = new ProblemGroup(signature, entry, log.Name);
                        bySignature.Add(signature, group);
                        groups.Add(group);
                    }
                    group.Occurrences += entry.Occurrences;
                    if (entry.Time < group.FirstTime) group.FirstTime = entry.Time;
                    if (entry.Time > group.LastTime) group.LastTime = entry.Time;
                    if (!group.Logs.Contains(log.Name)) group.Logs.Add(log.Name);
                }
            }
            Sort(groups);
            return groups;
        }

        /// <summary>Most severe first, then the most reports, then the most occurrences.</summary>
        public static void Sort(List<ProblemGroup> groups) =>
            groups.Sort((a, b) =>
            {
                int bySeverity = b.Severity.CompareTo(a.Severity);
                if (bySeverity != 0) return bySeverity;
                int byReports = b.Reports.Count.CompareTo(a.Reports.Count);
                return byReports != 0 ? byReports : b.Occurrences.CompareTo(a.Occurrences);
            });
    }
}
