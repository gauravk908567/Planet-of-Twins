using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace PoT.Diagnostics.Editor
{
    /// <summary>One line of a Unity stack trace.</summary>
    public readonly struct StackFrameInfo
    {
        /// <summary>The method as Unity writes it, e.g. <c>EnemySpawner:SpawnOne</c> or <c>Foo.Bar.Baz</c>.</summary>
        public readonly string Method;
        /// <summary>The source file, as the trace gives it (a project path, or a build machine's absolute path); may be empty.</summary>
        public readonly string File;
        /// <summary>The line in <see cref="File"/>, or 0.</summary>
        public readonly int Line;

        public StackFrameInfo(string method, string file, int line)
        {
            Method = method;
            File = file;
            Line = line;
        }

        /// <summary>Unity's own logging calls, which sit on top of every logged stack and say nothing about the cause.</summary>
        public bool IsLoggingCall =>
            Method.StartsWith("UnityEngine.Debug", StringComparison.Ordinal)
            || Method.StartsWith("UnityEngine.Logger", StringComparison.Ordinal)
            || Method.StartsWith("UnityEngine.DebugLogHandler", StringComparison.Ordinal)
            || Method.StartsWith("UnityEngine.StackTraceUtility", StringComparison.Ordinal);

        /// <summary>The class's short name: <c>A.B.Foo+&lt;Run&gt;d__3:MoveNext</c> → <c>Foo</c>.</summary>
        public string ClassName
        {
            get
            {
                string owner = Method;
                int colon = owner.IndexOf(':');
                if (colon >= 0) owner = owner.Substring(0, colon);
                else
                {
                    int lastDot = owner.LastIndexOf('.');   // exception style: Namespace.Class.Method
                    if (lastDot > 0) owner = owner.Substring(0, lastDot);
                }
                int plus = owner.IndexOf('+');
                if (plus >= 0) owner = owner.Substring(0, plus);
                int dot = owner.LastIndexOf('.');
                if (dot >= 0) owner = owner.Substring(dot + 1);
                int tick = owner.IndexOf('`');
                return tick >= 0 ? owner.Substring(0, tick) : owner;
            }
        }
    }

    /// <summary>Reads stack-trace lines and opens them in the code editor.</summary>
    public static class StackFrames
    {
        // "Class:Method (args)" or "Namespace.Class.Method (args)", optionally followed by the location.
        private static readonly Regex MethodPattern = new Regex(@"^([^\s()]+[:.][^\s()]+) ?\(", RegexOptions.CultureInvariant);
        private static readonly Regex AtPattern = new Regex(@"\(at (.+):(\d+)\)\s*$", RegexOptions.CultureInvariant);
        // Mono's exception style: "Foo.Bar () [0x00012] in C:\path\Foo.cs:42".
        private static readonly Regex InPattern = new Regex(@"[\)\]] in (.+):(\d+)\s*$", RegexOptions.CultureInvariant);

        /// <summary>Reads one trace line. False when the line isn't a stack frame (e.g. a message line).</summary>
        public static bool TryParse(string line, out StackFrameInfo frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(line)) return false;
            var method = MethodPattern.Match(line);
            if (!method.Success) return false;

            string file = string.Empty;
            int number = 0;
            var at = AtPattern.Match(line);
            if (!at.Success) at = InPattern.Match(line);
            if (at.Success)
            {
                file = at.Groups[1].Value;
                int.TryParse(at.Groups[2].Value, out number);
                if (file.StartsWith("<", StringComparison.Ordinal)) file = string.Empty;   // "<hash>:0": no source
            }
            frame = new StackFrameInfo(method.Groups[1].Value, file, number);
            return true;
        }

        /// <summary>
        /// Finds the project script a frame points at: its path (a build machine's absolute path is matched from its
        /// <c>Assets/</c> or <c>Packages/</c> part), otherwise a script named after the frame's class (IL2CPP builds
        /// often have no file names). Null when the project has no such script.
        /// </summary>
        public static MonoScript Resolve(in StackFrameInfo frame)
        {
            if (!string.IsNullOrEmpty(frame.File))
            {
                string path = frame.File.Replace('\\', '/');
                foreach (var root in new[] { "Assets/", "Packages/" })
                {
                    int at = path.IndexOf(root, StringComparison.Ordinal);
                    if (at < 0) continue;
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path.Substring(at));
                    if (script != null) return script;
                }
            }

            string className = frame.ClassName;
            if (string.IsNullOrEmpty(className)) return null;
            foreach (var guid in AssetDatabase.FindAssets(className + " t:MonoScript"))
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(candidate) == className)
                    return AssetDatabase.LoadAssetAtPath<MonoScript>(candidate);
            }
            return null;
        }

        /// <summary>Opens the frame's script at its line. False when the project has no such script.</summary>
        public static bool Open(in StackFrameInfo frame)
        {
            var script = Resolve(frame);
            if (script == null) return false;
            return AssetDatabase.OpenAsset(script, Math.Max(1, frame.Line));
        }
    }
}
