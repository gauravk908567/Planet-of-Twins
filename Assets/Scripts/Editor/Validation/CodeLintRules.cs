using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// Static lint over Assets/Scripts for documented Rulebook / Banned-Lazy-Work violations.
    /// Deliberately conservative + allowlisted so it earns trust (≈zero false positives). The
    /// Phase-9 pass is INFO-level ("candidate"), not an assertion of guilt.
    /// </summary>
    public static class CodeLintRules
    {
        private sealed class LintPattern
        {
            public Regex Regex;
            public string Category;
            public ValidationSeverity Severity;
            public string Message;
            public HashSet<string> AllowFiles;   // file names where the pattern is legitimate
            public string RequireFileContains;   // if set, only scan files whose text contains this token
        }

        public static void Collect(List<ValidationFinding> findings)
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            if (!Directory.Exists(scriptsRoot)) return;

            var patterns = BuildPatterns();

            foreach (var file in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                string[] lines;
                string fullText;
                try { fullText = File.ReadAllText(file); lines = fullText.Split('\n'); }
                catch { continue; }

                string assetPath = ToAssetPath(file);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                // line-level patterns — AGGREGATED per file (one row per file+pattern, not per line,
                // so a debug file with 30 raw-Input uses is one actionable finding, not 30).
                foreach (var pat in patterns)
                {
                    if (pat.AllowFiles != null && pat.AllowFiles.Contains(fileName)) continue;
                    if (pat.RequireFileContains != null && !fullText.Contains(pat.RequireFileContains)) continue;

                    int hits = 0, firstLine = 0;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string trimmed = lines[i].TrimStart();
                        if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue; // skip comments
                        if (!pat.Regex.IsMatch(lines[i])) continue;
                        if (hits == 0) firstLine = i + 1;
                        hits++;
                    }
                    if (hits == 0) continue;

                    string suffix = hits == 1 ? "" : $" ({hits} uses, first at line {firstLine})";
                    findings.Add(new ValidationFinding(pat.Severity, pat.Category, pat.Message + suffix,
                        script, $"{assetPath}:{firstLine}"));
                }

                // Phase-9 VFX candidate (per-file, INFO): Instantiate + a VFX type in the same file.
                if (!Phase9Allow.Contains(fileName)
                    && InstantiateRx.IsMatch(fullText)
                    && (fullText.Contains("ParticleSystem") || fullText.Contains("VisualEffect")))
                {
                    findings.Add(new ValidationFinding(ValidationSeverity.Info, "Code lint (Phase 9)",
 "Instantiates near a VFX type — candidate for migration onto FxManager.Play. Review.",
                        script, assetPath));
                }
            }
        }

        private static readonly Regex InstantiateRx = new Regex(@"\bInstantiate\s*\(", RegexOptions.Compiled);

        private static readonly HashSet<string> Phase9Allow = new HashSet<string>
        {
            "FxManager.cs", "VfxPool.cs", "AudioManager.cs", "FxLifetime.cs", "CueSequenceRunner.cs",
        };

        private static List<LintPattern> BuildPatterns() => new List<LintPattern>
        {
            new LintPattern
            {
                Regex = new Regex(@"\bDontDestroyOnLoad\s*\(", RegexOptions.Compiled),
                Category = "Code lint (R3)", Severity = ValidationSeverity.Warning,
                Message = "DontDestroyOnLoad is banned (R3) — it duplicates managers across the Restart→Bootstrap loop. Make the object live in Persistent.unity.",
                AllowFiles = null,
            },
            new LintPattern
            {
                Regex = new Regex(@"Time\.timeScale\s*=(?!=)", RegexOptions.Compiled),
                Category = "Code lint (R10)", Severity = ValidationSeverity.Warning,
                Message = "Direct Time.timeScale write — route through TimeScaleService (min-value-wins, R10). Never add an eighth raw writer.",
                AllowFiles = new HashSet<string> { "TimeScaleService.cs" },
            },
            new LintPattern
            {
                // Action input only (gate-relevant). mousePosition is pointer position, not gated — excluded.
                Regex = new Regex(@"\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetButton|GetButtonDown|GetButtonUp|GetAxis|GetAxisRaw|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp)", RegexOptions.Compiled),
                Category = "Code lint (Input)", Severity = ValidationSeverity.Warning,
                Message = "Raw Input.* bypasses TutorialInputGate — extend IInputProvider / TwinInputReader instead.",
                AllowFiles = new HashSet<string> { "TwinInputReader.cs" },
            },
            new LintPattern
            {
                Regex = new Regex(@"\.TransitionTo\s*\(", RegexOptions.Compiled),
                Category = "Code lint (FX)", Severity = ValidationSeverity.Warning,
                Message = "AudioMixerSnapshot.TransitionTo outside the snapshot arbiter (Banned Lazy Work #11) — route through AudioManager's arbiter.",
                AllowFiles = new HashSet<string> { "AudioManager.cs", "SnapshotArbiter.cs" },
                RequireFileContains = "AudioMixerSnapshot", // only real audio-snapshot transitions, not state-machine TransitionTo
            },
        };

        private static string ToAssetPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            return absolute.StartsWith(data) ? "Assets" + absolute.Substring(data.Length) : absolute;
        }
    }
}
