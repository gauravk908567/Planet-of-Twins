#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PoT.Diagnostics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the session log's size and flood limits (BUG-139):
///   • <see cref="BoundedLogFile"/> with small limits: after thousands of entries the file is under its limit, keeps
///     its head and its newest entries whole and in order, and holds one marker where older lines were removed;
///   • <see cref="LogMessageTracker"/>: a message whose numbers change counts as one; 20 copies per 10 s are written,
///     the rest held back and reported; the ledger lists it with its total, most severe first;
///   • in Play mode, also the real session log: 60 copies of one warning → 20 in the file, 60 in the ledger.
///
/// Run: <b>Planet of Twins Tools ▸ Diagnostics ▸ Test Session Log Limits</b> (Edit or Play mode).
/// </summary>
public static class DiagnosticsSessionLogTest
{
    private const string Marker = "of older lines removed here";

    [MenuItem("Planet of Twins Tools/Diagnostics/Test Session Log Limits")]
    public static void Run()
    {
        var problems = new List<string>();
        var notes = new List<string>();
        CheckBoundedFile(problems, notes);
        CheckTracker(problems);
        if (Application.isPlaying) CheckLiveLog(problems, notes);
        else notes.Add("Play mode not running: the live session-log check was skipped.");

        string summary = "[Diagnostics session log] " + string.Join(" ", notes);
        if (problems.Count == 0) Debug.Log(summary + "\nSelf-check PASS.");
        else Debug.LogError(summary + "\nSelf-check FAIL:\n  " + string.Join("\n  ", problems));
    }

    // ── BoundedLogFile ──────────────────────────────────────────────────────────

    private static void CheckBoundedFile(List<string> problems, List<string> notes)
    {
        const long max = 64 * 1024, head = 8 * 1024, tail = 16 * 1024;
        const int entries = 3000;
        string folder = Path.Combine(Path.GetTempPath(), "PoT_SessionLogCheck");
        string path = Path.Combine(folder, "bounded_check.log");
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);

            int trims;
            long removed;
            using (var file = new BoundedLogFile(path, max, head, tail))
            {
                file.WriteLine("=== header ===");
                file.EndEntry();
                for (int n = 1; n <= entries; n++)
                {
                    file.WriteLine(EntryLine(n));
                    if (n % 3 == 0)
                    {
                        file.WriteLine(StackLine(n, 'a'));
                        file.WriteLine(StackLine(n, 'b'));
                    }
                    file.EndEntry();
                    if (file.Length > max) problems.Add($"bounded: {file.Length} bytes after entry {n}, over {max}");
                    if (file.Stopped) { problems.Add("bounded: stopped: " + file.StopReason); break; }
                }
                trims = file.Trims;
                removed = file.RemovedBytes;
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            var lines = new List<string>(text.Split('\n'));
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);

            if (trims == 0 || removed <= 0) problems.Add("bounded: never trimmed");
            if (lines.Count == 0 || lines[0] != "=== header ===") problems.Add("bounded: the header line is gone");

            int markerAt = -1, markers = 0;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].IndexOf(Marker, StringComparison.Ordinal) >= 0) { markers++; markerAt = i; }
            if (markers != 1) { problems.Add($"bounded: {markers} marker lines, expected 1"); return; }

            // Head: entries 1.. in order, whole, at least `head` bytes. Tail: consecutive entries up to the last one.
            int headLast = CheckRun(lines, 1, markerAt, 1, problems, "head");
            int tailFirst = ParseEntry(lines[markerAt + 1]);
            if (tailFirst < 0) problems.Add($"bounded: the line after the marker is not an entry's first line: '{lines[markerAt + 1]}'");
            else if (CheckRun(lines, markerAt + 1, lines.Count, tailFirst, problems, "tail") != entries)
                problems.Add($"bounded: the tail doesn't end with entry {entries}");

            long headBytes = Encoding.UTF8.GetByteCount(string.Join("\n", lines.GetRange(0, markerAt))) + 1;
            long tailBytes = Encoding.UTF8.GetByteCount(string.Join("\n", lines.GetRange(markerAt + 1, lines.Count - markerAt - 1))) + 1;
            if (headBytes < head) problems.Add($"bounded: head is {headBytes} bytes, under {head}");
            if (tailBytes < tail - 512) problems.Add($"bounded: kept tail is {tailBytes} bytes, well under {tail}");

            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "Bounded file: {0} entries → {1:0.0} KB (limit {2} KB), {3} trims, head = entries 1–{4}, tail = entries {5}–{6}.",
                entries, new FileInfo(path).Length / 1024.0, max / 1024, trims, headLast, tailFirst, entries));
        }
        catch (Exception e)
        {
            problems.Add("bounded: the check failed to run: " + e.Message);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (Exception) { /* temp folder: leave it */ }
        }
    }

    // Lines [from, to) must be consecutive entries starting at `first`, every third with its two stack lines.
    // Returns the last entry number seen.
    private static int CheckRun(List<string> lines, int from, int to, int first, List<string> problems, string part)
    {
        var whole = new Regex(@"^entry \d{5} [.]{60} æ$");
        int expected = first, last = first - 1;
        for (int i = from; i < to; i++)
        {
            string line = lines[i];
            if (!whole.IsMatch(line)) { problems.Add($"bounded {part}: not a whole entry line at {i}: '{line}'"); return last; }
            int n = ParseEntry(line);
            if (n != expected) { problems.Add($"bounded {part}: entry {n} where {expected} was expected"); return last; }
            if (n % 3 == 0)
            {
                if (i + 2 >= to || lines[i + 1] != StackLine(n, 'a') || lines[i + 2] != StackLine(n, 'b'))
                {
                    problems.Add($"bounded {part}: entry {n} lost its stack lines");
                    return last;
                }
                i += 2;
            }
            last = n;
            expected++;
        }
        return last;
    }

    private static string EntryLine(int n) =>
        "entry " + n.ToString("00000", CultureInfo.InvariantCulture) + " " + new string('.', 60) + " æ";

    private static string StackLine(int n, char which) =>
        "    at Stack.Frame " + n.ToString("00000", CultureInfo.InvariantCulture) + " " + which;

    private static int ParseEntry(string line) =>
        line.StartsWith("entry ", StringComparison.Ordinal) && line.Length >= 11 &&
        int.TryParse(line.Substring(6, 5), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : -1;

    // ── LogMessageTracker ───────────────────────────────────────────────────────

    private static void CheckTracker(List<string> problems)
    {
        var inv = CultureInfo.InvariantCulture;
        var tracker = new LogMessageTracker(20, 10, 8);

        int written = 0;
        for (int i = 0; i < 1000; i++)
        {
            string message = string.Format(inv, "[Test] dist {0:0.00} (Enemy {1})", i * 0.37, i % 13);
            if (tracker.Allow("WARNING", message, "at Test.Stack ()", i * 0.01, out _, out _)) written++;
        }
        if (written != 20) problems.Add($"tracker: {written} of 1000 copies written in one 10 s window, expected 20");
        if (tracker.HeldBackLines != 980) problems.Add($"tracker: {tracker.HeldBackLines} held back, expected 980");
        if (tracker.Entries.Count != 1) problems.Add($"tracker: {tracker.Entries.Count} messages, expected 1 (numbers must not count)");

        bool next = tracker.Allow("WARNING", "[Test] dist 1.00 (Enemy 1)", null, 10.5, out int held, out _);
        if (!next || held != 980) problems.Add($"tracker: after the window, written={next} held={held}, expected true/980");

        tracker.Count("WARNING", "[Test] dist 2.00 (Enemy 2)", 10.6);
        tracker.Allow("ERROR", "[Test] something broke", "at Test.Broken ()", 11, out _, out _);
        for (char c = 'a'; c <= 'l'; c++)   // 12 new messages: 6 fit (8 max), 6 untracked
            if (!tracker.Allow("WARNING", "[Test] other " + c, null, 12, out _, out _))
                problems.Add("tracker: an untracked or new message was held back");
        if (tracker.Entries.Count != 8) problems.Add($"tracker: {tracker.Entries.Count} messages tracked, expected 8");
        if (tracker.Untracked != 6) problems.Add($"tracker: {tracker.Untracked} untracked, expected 6");

        var ledger = tracker.Describe(3);
        string text = string.Join("\n", ledger);
        if (!ledger[0].StartsWith("Problems this session: 8 different", StringComparison.Ordinal))
            problems.Add("tracker: ledger title: " + ledger[0]);
        if (ledger.Count < 2 || !ledger[1].StartsWith("ERROR ×1", StringComparison.Ordinal))
            problems.Add("tracker: the ERROR is not listed first");
        if (text.IndexOf("WARNING ×1,002, 980 held back", StringComparison.Ordinal) < 0)
            problems.Add("tracker: the flood message's line is wrong:\n" + text);
        if (text.IndexOf("and 5 more different messages", StringComparison.Ordinal) < 0)
            problems.Add("tracker: the '(and N more)' line is missing");
    }

    // ── The real session log (Play mode) ────────────────────────────────────────

    private static void CheckLiveLog(List<string> problems, List<string> notes)
    {
        string path = SessionLog.CurrentPath;
        if (path == null) { problems.Add("live: no session log is open"); return; }

        string nonce = RandomLetters(8);   // letters only: every run is a new message, digits would be folded
        const int copies = 60;
        for (int i = 1; i <= copies; i++)
            Debug.LogWarning($"[Diagnostics self-test {nonce}] flood copy {i} of {copies}");

        string text;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            text = reader.ReadToEnd();
        int inFile = Regex.Matches(text, "self-test " + nonce + @"\] flood copy").Count;
        if (inFile != SessionLog.FloodBurst)
            problems.Add($"live: {inFile} copies in the session log, expected {SessionLog.FloodBurst}");

        string ledger = SessionLog.DescribeProblems();
        int at = ledger.IndexOf(nonce, StringComparison.Ordinal);
        string expected = $"WARNING ×{copies}, {copies - SessionLog.FloodBurst} held back";
        if (at < 0) problems.Add("live: the warning is missing from the problem ledger");
        else if (ledger.LastIndexOf(expected, at, StringComparison.Ordinal) < 0)
            problems.Add($"live: the ledger line before the sample isn't '{expected}'");

        notes.Add($"Live log: {inFile} of {copies} copies written; {SessionLog.DescribeLimits()}.");
    }

    private static string RandomLetters(int count)
    {
        const string letters = "abcdefghijkmnpqrstuvwxyz";
        var random = new System.Random();
        var result = new StringBuilder(count);
        for (int i = 0; i < count; i++) result.Append(letters[random.Next(letters.Length)]);
        return result.ToString();
    }
}
#endif
