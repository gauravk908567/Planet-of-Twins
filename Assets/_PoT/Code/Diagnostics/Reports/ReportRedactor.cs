using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Removes what identifies the player's PC from report text (game.md §27.3):
    ///   • the user-profile path (<c>C:\Users\name</c>) in every spelling logs use: back slashes, forward slashes,
    ///     JSON-escaped, and the 8.3 short form Windows gives the temp folder (<c>C:\Users\NAME~1</c>) → <c>&lt;user&gt;</c>;
    ///   • the Windows user name and the machine name as whole words → <c>&lt;user&gt;</c> / <c>&lt;machine&gt;</c>.
    ///     Names shorter than 3 characters are left alone: replacing them would mangle ordinary words;
    ///   • email addresses → <c>&lt;email&gt;</c> (a domain must start with a letter, so package ids such as
    ///     <c>com.unity.x@1.2.3</c> survive).
    /// Case-insensitive. Immutable after construction, so any thread may use it.
    /// </summary>
    internal sealed class ReportRedactor
    {
        internal const string UserToken = "<user>";
        internal const string MachineToken = "<machine>";
        internal const string EmailToken = "<email>";

        private const int MinNameLength = 3;
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex Email =
            new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z][A-Za-z0-9-]*(?:\.[A-Za-z][A-Za-z0-9-]*)*\.[A-Za-z]{2,}", Options);

        private readonly Regex _paths;   // null when there is no profile path to look for
        private readonly Regex _names;   // null when both names are too short

        internal ReportRedactor(IEnumerable<string> profilePaths, string userName, string machineName)
        {
            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in profilePaths) AddVariants(variants, path);
            if (variants.Count > 0)
                _paths = new Regex(string.Join("|", variants.OrderByDescending(v => v.Length).Select(Regex.Escape)), Options);

            var names = new List<string>();
            if (IsLongEnough(userName)) names.Add($"(?<u>{Regex.Escape(userName)})");
            if (IsLongEnough(machineName) && !string.Equals(machineName, userName, StringComparison.OrdinalIgnoreCase))
                names.Add($"(?<m>{Regex.Escape(machineName)})");
            if (names.Count > 0)
                _names = new Regex(@"(?<![\p{L}\p{N}])(?:" + string.Join("|", names) + @")(?![\p{L}\p{N}])", Options);
        }

        /// <summary>The redactor for the PC this runs on. Call on the main thread before writing a report.</summary>
        internal static ReportRedactor ForThisMachine()
        {
            var profiles = new List<string>
            {
                Safe(() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
                ProfileFromTemp(Safe(Path.GetTempPath)),
            };
            return new ReportRedactor(profiles.Where(p => !string.IsNullOrEmpty(p)),
                                      Safe(() => Environment.UserName), Safe(() => Environment.MachineName));
        }

        internal string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (_paths != null) text = _paths.Replace(text, UserToken);
            if (_names != null) text = _names.Replace(text, NameToken);
            return Email.Replace(text, EmailToken);
        }

        private static string NameToken(Match match) => match.Groups["u"].Success ? UserToken : MachineToken;

        private static bool IsLongEnough(string name) => !string.IsNullOrWhiteSpace(name) && name.Length >= MinNameLength;

        private static void AddVariants(HashSet<string> into, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            path = path.TrimEnd('\\', '/');
            if (path.Length < MinNameLength) return;
            into.Add(path);
            into.Add(path.Replace('\\', '/'));
            into.Add(path.Replace("\\", "\\\\"));   // inside JSON strings
        }

        // The temp folder can hold the 8.3 short spelling of the profile (C:\Users\ABCDEF~1\AppData\Local\Temp).
        private static string ProfileFromTemp(string temp)
        {
            if (string.IsNullOrEmpty(temp)) return null;
            int cut = temp.IndexOf(@"\AppData\", StringComparison.OrdinalIgnoreCase);
            return cut > 0 ? temp.Substring(0, cut) : null;
        }

        private static string Safe(Func<string> read)
        {
            try { return read(); }
            catch (Exception) { return null; }   // a locked-down PC may refuse; that value is just not redacted
        }
    }
}
