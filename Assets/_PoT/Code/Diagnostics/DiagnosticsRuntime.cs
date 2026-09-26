using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Starts diagnostics before the first scene loads, with no GameObject (nothing to keep alive across scenes):
    /// the session log with its header, the crash marker, Unity's log hook, automatic breadcrumbs (scene
    /// load/unload/active, focus, low memory) and a clean close on quit. Statics are reset at
    /// SubsystemRegistration, so an Editor session with domain reload off starts clean.
    /// Spec: game.md §27.3.
    /// </summary>
    public static class DiagnosticsRuntime
    {
        public const string LogsFolderName = "Logs";
        public const string CategoryScene = "Scene";
        public const string CategoryApp = "App";

        /// <summary><c>persistentDataPath/Logs</c>: session logs, the crash lock, <c>PendingCrash/</c>.</summary>
        public static string LogsFolder { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Unhook();
            SessionLog.ResetStatics();
            Breadcrumbs.Clear();
            CrashMarker.ResetStatics();
            LogsFolder = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            DiagnosticsClock.Start();
            LogsFolder = Path.Combine(Application.persistentDataPath, LogsFolderName);

            // The crashed run's log is the newest one, so look before this run's file exists.
            string previousLog = SessionLog.FindNewest(LogsFolder);
            bool crashed = CrashMarker.Arm(LogsFolder, previousLog);

            var header = new List<(string Key, string Value)>
            {
                ("Session start (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                ("Session start (local)", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
            };
            SystemSnapshot.AppendApp(header);
            SystemSnapshot.AppendSystem(header);
            SessionLog.Open(LogsFolder, header);

            if (crashed)
                SessionLog.WriteNote("The previous session did not end cleanly (crash or forced close). " +
                                     "Its logs were kept for a bug report.");
            else if (CrashMarker.HasPendingCrash)
                SessionLog.WriteNote("An unsent crash report from an earlier session is pending.");

            Hook();
        }

        // ── Hooks (named handlers, R8) ──────────────────────────────────────────

        private static void Hook()
        {
            Application.logMessageReceivedThreaded += SessionLog.OnUnityLog;
            Application.quitting += OnQuitting;
            Application.focusChanged += OnFocusChanged;
            Application.lowMemory += OnLowMemory;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void Unhook()
        {
            Application.logMessageReceivedThreaded -= SessionLog.OnUnityLog;
            Application.quitting -= OnQuitting;
            Application.focusChanged -= OnFocusChanged;
            Application.lowMemory -= OnLowMemory;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        // Unity passes an undocumented mode value for the Editor's first Play scene, so print the two real ones.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) =>
            Breadcrumbs.Add(CategoryScene, $"loaded '{scene.name}' ({(mode == LoadSceneMode.Additive ? "additive" : "single")})");

        private static void OnSceneUnloaded(Scene scene) =>
            Breadcrumbs.Add(CategoryScene, $"unloaded '{scene.name}'");

        private static void OnActiveSceneChanged(Scene from, Scene to) =>
            Breadcrumbs.Add(CategoryScene, $"active → '{to.name}'");

        private static void OnFocusChanged(bool focused) =>
            Breadcrumbs.Add(CategoryApp, focused ? "focus gained" : "focus lost");

        private static void OnLowMemory() => Breadcrumbs.Add(CategoryApp, "low memory warning");

        // In the Editor this fires when Play stops.
        private static void OnQuitting()
        {
            Breadcrumbs.Add(CategoryApp, "quit");
            Unhook();
            CrashMarker.Disarm();
            SessionLog.Close("clean quit");
        }
    }
}
