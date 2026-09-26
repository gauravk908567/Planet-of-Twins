using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// The commit a player build was made from. After a successful build the editor writes
    /// <see cref="FileName"/> into the build's data folder (<c>&lt;Game&gt;_Data/</c>, which is
    /// <c>Application.dataPath</c> at runtime) as <c>key: value</c> lines: Commit, Branch, Local changes,
    /// Built (UTC). Written by PoT.Diagnostics.Editor's <c>BuildStampWriter</c>; nothing is added to the project,
    /// so there's no generated asset to keep out of git. The Editor has no stamp.
    /// </summary>
    public static class BuildStamp
    {
        public const string FileName = "build_stamp.txt";

        private static List<(string Key, string Value)> _fields;   // read once: a build's stamp never changes

        /// <summary>Adds the stamp's lines, or one line saying why there is none.</summary>
        public static void AppendTo(List<(string Key, string Value)> into)
        {
            if (Application.isEditor) { into.Add(("Commit", "(editor)")); return; }
            if (_fields == null) _fields = Read(Path.Combine(Application.dataPath, FileName));
            if (_fields.Count == 0) into.Add(("Commit", "unknown (no build stamp)"));
            else into.AddRange(_fields);
        }

        private static List<(string Key, string Value)> Read(string path)
        {
            var fields = new List<(string Key, string Value)>();
            try
            {
                if (!File.Exists(path)) return fields;
                foreach (var line in File.ReadAllLines(path))
                {
                    int colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (colon > 0) fields.Add((line.Substring(0, colon), line.Substring(colon + 2)));
                }
            }
            catch (Exception) { fields.Clear(); }   // unreadable: report "no build stamp"
            return fields;
        }
    }
}
