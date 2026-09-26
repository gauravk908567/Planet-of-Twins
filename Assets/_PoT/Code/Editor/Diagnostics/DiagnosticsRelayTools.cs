#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PoT.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Editor tools for sending reports (game.md §27.4, phase 4). Deploy steps for the relay: Tools/ReportRelay/README.md.
///   • <b>Wire Diagnostics Config</b>: creates <c>DiagnosticsConfig.asset</c> (<see cref="PoTPaths.Create.DiagnosticsData"/>)
///     if there is none, and assigns it to the report screen prefab when its slot is empty. Idempotent.
///   • <b>Relay: Check Deployment (no email)</b>: a GET to the relay → it should answer "relay alive".
///   • <b>Relay: Send Test Report</b>: builds a real report zip (like Build Test Report) and sends it through the same
///     uploader the game uses, logging every step (the 302-redirect spike). An email with the zip should arrive.
/// Both relay tools work in Edit and Play mode.
/// </summary>
public static class DiagnosticsRelayTools
{
    private const string Tag = "[Diagnostics relay]";

    // ── Config ────────────────────────────────────────────────────────
    [MenuItem("Planet of Twins Tools/Diagnostics/Wire Diagnostics Config")]
    public static void WireConfig()
    {
        var config = FindOrCreateConfig();
        if (config == null) return;

        var prefab = PoTAssetLookup.FindUnique<GameObject>(PoTPaths.Named.ReportScreenPrefab);
        var screen = prefab != null ? prefab.GetComponent<ReportProblemScreen>() : null;
        if (screen == null)
        {
            Debug.LogError($"{Tag} The report screen prefab has no ReportProblemScreen; nothing wired.", prefab);
            return;
        }
        var so = new SerializedObject(screen);
        var slot = so.FindProperty("_config");
        if (slot.objectReferenceValue == null)
        {
            slot.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Assigned {AssetDatabase.GetAssetPath(config)} to {AssetDatabase.GetAssetPath(prefab)}.", config);
        }
        else Debug.Log($"{Tag} The report screen prefab already uses {AssetDatabase.GetAssetPath(slot.objectReferenceValue)}.", config);

        Debug.Log(config.HasRelay
            ? $"{Tag} Relay URL set ({Host(config.RelayUrl)}). Fallback email: {(config.HasFallbackEmail ? config.FallbackEmail : "none")}."
            : $"{Tag} No relay URL yet: reports stay on the PC. Deploy the relay (Tools/ReportRelay/README.md) and paste its URL.",
            config);
        Selection.activeObject = config;
    }

    private static DiagnosticsConfig FindOrCreateConfig()
    {
        var existing = PoTAssetLookup.PathsOf<DiagnosticsConfig>(PoTPaths.Named.DiagnosticsConfig);
        if (existing.Count == 1) return AssetDatabase.LoadAssetAtPath<DiagnosticsConfig>(existing[0]);
        if (existing.Count > 1)
        {
            Debug.LogError($"{Tag} {existing.Count} DiagnosticsConfig assets ({string.Join(", ", existing)}): keep one.");
            return null;
        }
        if (!AssetDatabase.IsValidFolder(PoTPaths.Create.DiagnosticsData))
        {
            string parent = Path.GetDirectoryName(PoTPaths.Create.DiagnosticsData).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(PoTPaths.Create.DiagnosticsData));
        }
        var config = ScriptableObject.CreateInstance<DiagnosticsConfig>();
        string path = $"{PoTPaths.Create.DiagnosticsData}/{PoTPaths.Named.DiagnosticsConfig}.asset";
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} Created {path}.", config);
        return config;
    }

    private static DiagnosticsConfig RequireRelay()
    {
        var config = PoTAssetLookup.FindUnique<DiagnosticsConfig>(PoTPaths.Named.DiagnosticsConfig);
        if (config == null) return null;   // FindUnique logged why
        if (!config.HasRelay)
        {
            Debug.LogError($"{Tag} DiagnosticsConfig has no relay URL (it must start with https://). " +
                           "Deploy the relay (Tools/ReportRelay/README.md) and paste its URL.", config);
            return null;
        }
        return config;
    }

    // ── Relay checks ──────────────────────────────────────────────────
    [MenuItem("Planet of Twins Tools/Diagnostics/Relay: Check Deployment (no email)")]
    public static void CheckDeployment()
    {
        var config = RequireRelay();
        if (config != null) EditorRoutine.Start(CheckRoutine(config));
    }

    private static IEnumerator CheckRoutine(DiagnosticsConfig config)
    {
        using (var get = UnityWebRequest.Get(config.RelayUrl))
        {
            get.timeout = config.TimeoutSeconds;
            yield return get.SendWebRequest();
            string body = get.downloadHandler != null ? get.downloadHandler.text : string.Empty;
            string line = $"GET {Host(config.RelayUrl)} → {get.responseCode} {get.result}" +
                          (string.IsNullOrEmpty(get.error) ? "" : $" ({get.error})") + $"\n{Preview(body)}";
            if (get.result == UnityWebRequest.Result.Success && body.Contains("relay alive"))
                Debug.Log($"{Tag} Deployment check PASS: the relay answers.\n{line}");
            else
                Debug.LogError($"{Tag} Deployment check FAIL. Is the deployment's access set to \"Anyone\" and the URL the " +
                               $"one ending in /exec?\n{line}");
        }
    }

    [MenuItem("Planet of Twins Tools/Diagnostics/Relay: Send Test Report")]
    public static void SendTestReport()
    {
        var config = RequireRelay();
        if (config == null) return;
        var request = new ReportRequest
        {
            Description = "Relay test from the Editor (Planet of Twins Tools ▸ Diagnostics ▸ Relay: Send Test Report).",
            Category = "Other",
            Sections = PoTReportSections.All,
        };
        string zip = ReportCollector.BuildZip(request, out var package);
        Debug.Log($"{Tag} Built {package.ReportId} ({new FileInfo(zip).Length / 1024.0:0.0} KB); sending…");
        EditorRoutine.Start(SendRoutine(config, package, zip));
    }

    private static IEnumerator SendRoutine(DiagnosticsConfig config, ReportPackage package, string zip)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ReportUploadResult result = null;
        yield return config.CreateUploader().Send(package, zip, r => result = r);
        string timing = string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", watch.Elapsed.TotalSeconds);
        if (result != null && result.Sent)
            Debug.Log($"{Tag} Send PASS: {package.ReportId} sent in {timing} (\"{result.Message}\"). Check the inbox for " +
                      $"\"[PoT] {package.ReportId}\".\n{result.TraceText}");
        else if (result != null && result.Outcome == ReportUploadResult.Status.Unconfirmed)
            Debug.LogWarning($"{Tag} Send UNCONFIRMED after {timing}: {result.Message}. Check the inbox for " +
                             $"\"[PoT] {package.ReportId}\" (it most likely arrived).\n{result.TraceText}");
        else
            Debug.LogError($"{Tag} Send FAIL after {timing}: {result?.Message ?? "no result"}\n{result?.TraceText}");
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static string Host(string url)
    {
        try { return new Uri(url).Host; }
        catch (Exception) { return "?"; }
    }

    private static string Preview(string text)
    {
        text = (text ?? string.Empty).Replace('\n', ' ');
        return text.Length <= 300 ? text : text.Substring(0, 300) + " …";
    }

    /// <summary>Runs a coroutine from EditorApplication.update (Edit or Play mode): yields of null wait a tick, an
    /// AsyncOperation waits until done, a nested IEnumerator runs to its end first.</summary>
    private sealed class EditorRoutine
    {
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private AsyncOperation _waiting;

        public static void Start(IEnumerator routine) => new EditorRoutine(routine);

        private EditorRoutine(IEnumerator routine)
        {
            _stack.Push(routine);
            EditorApplication.update += Tick;
        }

        private void Tick()
        {
            if (_waiting != null && !_waiting.isDone) return;
            _waiting = null;
            while (_stack.Count > 0)
            {
                var top = _stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EditorApplication.update -= Tick;
                    return;
                }
                if (!more) { _stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { _stack.Push(nested); continue; }
                if (top.Current is AsyncOperation operation) _waiting = operation;
                return;
            }
            EditorApplication.update -= Tick;
        }
    }
}
#endif
