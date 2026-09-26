#if UNITY_EDITOR
using System.IO;
using PoT.Diagnostics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Player Settings the diagnostics system depends on (game.md §27.8):
///   • the company name, which decides persistentDataPath (saves, session logs, Unity's crash folder) and where
///     PlayerPrefs live (settings, rebinds);
///   • stack traces: plain logs skip the stack walk, warnings/errors/exceptions keep script traces for reports.
/// Idempotent: run it any time as a check. It changes only what differs and logs each change.
///
/// Run: <b>Planet of Twins Tools ▸ Diagnostics ▸ Apply Player Settings</b>.
/// </summary>
public static class DiagnosticsPlayerSettings
{
    public const string CompanyName = "æ-Ra";

    private static readonly (LogType Type, StackTraceLogType Trace)[] StackTraces =
    {
        (LogType.Log,       StackTraceLogType.None),
        (LogType.Warning,   StackTraceLogType.ScriptOnly),
        (LogType.Error,     StackTraceLogType.ScriptOnly),
        (LogType.Assert,    StackTraceLogType.ScriptOnly),
        (LogType.Exception, StackTraceLogType.ScriptOnly),
    };

    [MenuItem("Planet of Twins Tools/Diagnostics/Apply Player Settings")]
    public static void Apply()
    {
        int changes = 0;

        if (PlayerSettings.companyName != CompanyName)
        {
            Debug.Log($"[Diagnostics settings] Company name '{PlayerSettings.companyName}' → '{CompanyName}'. " +
                      "persistentDataPath (saves, logs) and PlayerPrefs move with it.");
            PlayerSettings.companyName = CompanyName;
            changes++;
        }

        foreach (var (type, trace) in StackTraces)
        {
            var current = PlayerSettings.GetStackTraceLogType(type);
            if (current == trace) continue;
            Debug.Log($"[Diagnostics settings] Stack trace for {type}: {current} → {trace}.");
            PlayerSettings.SetStackTraceLogType(type, trace);
            changes++;
        }

        if (changes > 0) AssetDatabase.SaveAssets();
        Debug.Log($"[Diagnostics settings] OK ({changes} change(s)): company '{PlayerSettings.companyName}', " +
                  $"stack traces Log=None, others ScriptOnly. Data folder: {Application.persistentDataPath}");
    }

    [MenuItem("Planet of Twins Tools/Diagnostics/Open Logs Folder")]
    public static void OpenLogsFolder()
    {
        string folder = Path.Combine(Application.persistentDataPath, DiagnosticsRuntime.LogsFolderName);
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }
}
#endif
