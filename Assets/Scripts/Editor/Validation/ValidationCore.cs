using System;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PlanetOfTwins.EditorTools
{
    public enum ValidationSeverity { Error, Warning, Info }

    /// <summary>One result row produced by a validation rule. <see cref="Fix"/> is optional.</summary>
    public sealed class ValidationFinding
    {
        public ValidationSeverity Severity;
        public string Category;      // "Cross-scene ref (R2)", "Null required", "Completeness", "WorldLocation", "Code lint"
        public string Message;
        public Object Context;       // pinged on Select (may be null for code-lint findings)
        public string ContextPath;   // file:line for code-lint, or scene name — shown as subtext
        public Action Fix;           // optional one-click fix
        public string FixLabel;      // button text when Fix != null

        // ── Waiver ("not required") — editor-only, set after each scan from SceneHealthWaivers ──
        public bool Waived;          // excluded from RecipeStatus; still shown as a neutral info line
        public string WaiverReason;  // optional ("" = marked without a reason)

        public ValidationFinding(ValidationSeverity severity, string category, string message,
                                 Object context = null, string contextPath = null)
        {
            Severity = severity;
            Category = category;
            Message = message;
            Context = context;
            ContextPath = contextPath;
        }

        public ValidationFinding WithFix(string label, Action fix)
        {
            FixLabel = label;
            Fix = fix;
            return this;
        }
    }

    /// <summary>Scene roles. Completeness rules apply ONLY to <see cref="Area"/> scenes and are conditional.</summary>
    public enum SceneKind { Bootstrap, Persistent, Intro, Area, Dev }

    public static class SceneClassifier
    {
        // Temp/merge/dev scenes that must never ship and are not validated as areas (CLAUDE.md, game.md).
        private static readonly string[] DevNames = { "SampleScene", "Restore", "Trees", "TestLab" };

        public static SceneKind Classify(Scene scene) => Classify(scene.name);

        public static SceneKind Classify(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return SceneKind.Dev;
            if (sceneName.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase)) return SceneKind.Bootstrap;
            if (sceneName.Equals("Persistent", StringComparison.OrdinalIgnoreCase)) return SceneKind.Persistent;
            if (sceneName.Equals("Intro", StringComparison.OrdinalIgnoreCase)) return SceneKind.Intro;
            foreach (var d in DevNames)
                if (sceneName.Equals(d, StringComparison.OrdinalIgnoreCase)) return SceneKind.Dev;
            return SceneKind.Area;
        }
    }
}
