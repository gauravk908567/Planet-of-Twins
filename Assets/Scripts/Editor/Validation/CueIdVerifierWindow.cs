using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// Tools ▸ Planet of Twins ▸ Cue Id Verifier — the safety net for the Cue Book's string ids. Scans every
    /// <c>PlayBook("id", …)</c> call site and every <see cref="CueBookData"/> asset, then reports:
    ///   • a PlayBook literal id that no book defines — a typo / wrong name at the call site (ERROR, with file:line);
    ///   • the same id defined twice inside one book (ERROR);
    ///   • a book id that no code literal references anywhere — renamed or dead (WARN).
    /// Direct-literal call sites are checked precisely; ids passed via a variable (e.g. a system that forwards
    /// a mood/state id) are counted as "dynamic" and validated through the all-literals coverage pass.
    /// </summary>
    public class CueIdVerifierWindow : EditorWindow
    {
        private const string ScriptRoot = "Assets/Scripts";
        private const string SelfFile = "CueIdVerifierWindow.cs";
        private const string FxIdsPath = "Assets/Scripts/Fx/Generated/FxIds.cs";

        // PlayBook(<book-expr-without-comma>, "id", …)
        private static readonly Regex PlayBookLiteral = new Regex(
            "PlayBook\\s*\\(\\s*[^,()]+,\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        // PlayBook(<book>, someVariable, …) — id not a literal
        private static readonly Regex PlayBookDynamic = new Regex(
            "PlayBook\\s*\\(\\s*[^,()]+,\\s*(?![\"@])([A-Za-z_]\\w*)\\s*,", RegexOptions.Compiled);
        private static readonly Regex AnyLiteral = new Regex("\"([^\"\\\\]{1,64})\"", RegexOptions.Compiled);

        private struct Finding { public MessageType type; public string msg; public Object ping; public string file; public int line; }
        private readonly List<Finding> _findings = new List<Finding>();
        private Vector2 _scroll;
        private int _errors, _warnings;

        [MenuItem("Tools/Planet of Twins/Cue Id Verifier")]
        public static void Open()
        {
            var w = GetWindow<CueIdVerifierWindow>("Cue Id Verifier");
            w.minSize = new Vector2(540, 400);
            w.Validate();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate", GUILayout.Height(26))) Validate();
                if (GUILayout.Button("Generate FxIds", GUILayout.Height(26))) GenerateFxIds();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_errors} error(s) · {_warnings} warning(s)", EditorStyles.boldLabel);
            }
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_findings.Count == 0)
                EditorGUILayout.HelpBox("No issues — every PlayBook id resolves to a Cue Book effect.", MessageType.Info);

            foreach (var f in _findings)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(f.msg, f.type);
                    if (f.ping != null)
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(38)))
                            EditorGUIUtility.PingObject(f.ping);
                    }
                    else if (!string.IsNullOrEmpty(f.file))
                    {
                        if (GUILayout.Button("Open", GUILayout.Width(60), GUILayout.Height(38)))
                            OpenAt(f.file, f.line);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void OpenAt(string file, int line)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(file);
            if (obj != null) AssetDatabase.OpenAsset(obj, line);
        }

        private void Validate()
        {
            _findings.Clear();
            _errors = _warnings = 0;

            // 1) ids defined per book (+ duplicate / empty detection)
            var definedIds = new HashSet<string>();
            var idToBook = new Dictionary<string, CueBookData>();
            foreach (var guid in AssetDatabase.FindAssets("t:CueBookData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var book = AssetDatabase.LoadAssetAtPath<CueBookData>(path);
                if (book == null || book.effects == null) continue;

                var seen = new HashSet<string>();
                foreach (var e in book.effects)
                {
                    if (e == null) continue;
                    if (string.IsNullOrWhiteSpace(e.id))
                    {
                        Add(MessageType.Warning, $"'{book.name}' has an effect with an empty id — code can't call it.", book);
                        continue;
                    }
                    if (!seen.Add(e.id))
                        Add(MessageType.Error, $"'{book.name}' defines id \"{e.id}\" more than once.", book);
                    definedIds.Add(e.id);
                    idToBook[e.id] = book;
                }

                // Author-time lint (timing / cut sanity) — shared analyzer with the Cue Book inspector.
                foreach (var lf in CueBookLinter.Analyze(book))
                {
                    var mt = lf.severity == CueBookLinter.Severity.Warning ? MessageType.Warning : MessageType.Info;
                    string where = lf.elementIndex >= 0
                        ? $"effect \"{lf.effectId}\" element #{lf.elementIndex}"
                        : $"effect \"{lf.effectId}\"";
                    Add(mt, $"'{book.name}' [{where}]: {lf.message}  Fix: {lf.fix}", book);
                }
            }

            // 2) scan code: direct PlayBook literals (precise) + every literal (coverage) + dynamic-id calls
            var allLiterals = new HashSet<string>();
            var playBookLiterals = new List<(string id, string file, int line)>();
            int dynamicCalls = 0;

            if (Directory.Exists(ScriptRoot))
            {
                foreach (var file in Directory.GetFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file) == SelfFile) continue;   // don't scan our own regex strings
                    string rel = file.Replace('\\', '/');
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        foreach (Match m in AnyLiteral.Matches(line)) allLiterals.Add(m.Groups[1].Value);
                        foreach (Match m in PlayBookLiteral.Matches(line))
                            playBookLiterals.Add((m.Groups[1].Value, rel, i + 1));
                        dynamicCalls += PlayBookDynamic.Matches(line).Count;
                    }
                }
            }

            // 3a) a PlayBook literal that no book defines → typo / wrong name at the call site
            foreach (var (id, file, line) in playBookLiterals)
                if (!definedIds.Contains(id))
                    Add(MessageType.Error,
                        $"PlayBook(\"{id}\") at {Path.GetFileName(file)}:{line} — no Cue Book defines that id.",
                        null, file, line);

            // 3b) a defined id referenced by no code literal → renamed or dead
            foreach (var id in definedIds)
                if (!allLiterals.Contains(id))
                    Add(MessageType.Warning,
                        $"Cue id \"{id}\" (in '{idToBook[id].name}') is referenced by no code literal — renamed or dead?",
                        idToBook[id]);

            if (dynamicCalls > 0)
                Add(MessageType.Info,
                    $"{dynamicCalls} PlayBook call(s) pass the id via a variable (not directly checkable). " +
                    "Their literal ids are still covered by the check above.", null);

            Repaint();
        }

        // Writes Fx/Generated/FxIds.cs. Output is NESTED BY DOMAIN, mirroring the VFX libraries: for every
        // *VfxLibrary SO, each CueBookData slot becomes FxIds.<Library>.<Slot>.<Id> (the id is written ONCE on the
        // book; the const is generated). Books not referenced by any library fall under FxIds.Unsorted.<Book>.<Id>
 // so nothing is lost. (+ the VFX Library layer, 2026-06-22.)
        private void GenerateFxIds()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// AUTO-GENERATED by the Cue Id Verifier (Tools ▸ Planet of Twins ▸ Cue Id Verifier ▸ Generate FxIds).");
 sb.AppendLine("// Do NOT edit by hand — regenerate after adding/renaming a Cue Book effect id.");
            sb.AppendLine("// Id is authored ONCE on the Cue Book; callers use these constants. Nested by VFX library/domain.");
            sb.AppendLine("public static class FxIds");
            sb.AppendLine("{");

            var booksInLibraries = new HashSet<CueBookData>();

            // 1) Domain-nested: one class per *VfxLibrary, one inner class per CueBookData slot it references.
            foreach (var libType in CollectLibraryTypes())
            {
                foreach (var libGuid in AssetDatabase.FindAssets("t:" + libType.Name))
                {
                    var lib = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(libGuid), libType) as ScriptableObject;
                    if (lib == null) continue;

                    // domain name = library type without the trailing "VfxLibrary" (PlayerVfxLibrary → Player)
                    string domain = Sanitize(StripSuffix(libType.Name, "VfxLibrary"));
                    sb.AppendLine($"    public static class {domain}");
                    sb.AppendLine("    {");

                    foreach (var field in libType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (field.FieldType != typeof(CueBookData)) continue;
                        var book = field.GetValue(lib) as CueBookData;
                        if (book == null) continue;
                        booksInLibraries.Add(book);
                        EmitBookClass(sb, Sanitize(field.Name), book, indent: "        ");
                    }

                    sb.AppendLine("    }");
                }
            }

            // 2) Anything not in a library still gets constants (back-compat / un-migrated consumers).
            var orphans = new List<CueBookData>();
            foreach (var guid in AssetDatabase.FindAssets("t:CueBookData"))
            {
                var book = AssetDatabase.LoadAssetAtPath<CueBookData>(AssetDatabase.GUIDToAssetPath(guid));
                if (book == null || book.effects == null || book.effects.Count == 0) continue;
                if (!booksInLibraries.Contains(book)) orphans.Add(book);
            }
            if (orphans.Count > 0)
            {
                sb.AppendLine("    public static class Unsorted");
                sb.AppendLine("    {");
                foreach (var book in orphans)
                    EmitBookClass(sb, Sanitize(book.name), book, indent: "        ");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");

            string dir = System.IO.Path.GetDirectoryName(FxIdsPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(FxIdsPath, sb.ToString());
            AssetDatabase.ImportAsset(FxIdsPath);
            Debug.Log($"[CueIdVerifier] Generated {FxIdsPath} ({booksInLibraries.Count} book(s) in libraries, {orphans.Count} unsorted).");
        }

        // One inner class per book: a const per id (id written once on the book; first const wins on collision).
        private static void EmitBookClass(System.Text.StringBuilder sb, string className, CueBookData book, string indent)
        {
            sb.AppendLine($"{indent}public static class {className}");
            sb.AppendLine($"{indent}{{");
            var seen = new HashSet<string>();
            foreach (var e in book.effects)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.id)) continue;
                string c = Sanitize(e.id);
                if (!seen.Add(c)) continue;   // the verifier separately flags duplicate ids
                sb.AppendLine($"{indent}    public const string {c} = \"{e.id}\";");
            }
            sb.AppendLine($"{indent}}}");
        }

        // Every concrete type whose name ends in "VfxLibrary" (PlayerVfxLibrary, EnemyVfxLibrary, …).
        private static List<System.Type> CollectLibraryTypes()
        {
            var result = new List<System.Type>();
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var ty in types)
                    if (typeof(ScriptableObject).IsAssignableFrom(ty) && !ty.IsAbstract && ty.Name.EndsWith("VfxLibrary"))
                        result.Add(ty);
            }
            return result;
        }

        private static string StripSuffix(string s, string suffix)
            => s.EndsWith(suffix) ? s.Substring(0, s.Length - suffix.Length) : s;

        // Make a book/asset name or effect id a valid C# identifier (class/const name).
        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "_";
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            string s = sb.ToString();
            return char.IsDigit(s[0]) ? "_" + s : s;
        }

        private void Add(MessageType t, string msg, Object ping, string file = null, int line = 0)
        {
            _findings.Add(new Finding { type = t, msg = msg, ping = ping, file = file, line = line });
            if (t == MessageType.Error) _errors++;
            else if (t == MessageType.Warning) _warnings++;
        }
    }
}
