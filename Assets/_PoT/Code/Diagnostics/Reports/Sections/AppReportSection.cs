using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PoT.Diagnostics
{
    /// <summary>
    /// <c>app</c>: which build (version, commit stamp, build type), how long this session has run, the time scale,
    /// the loaded scenes and which log channels are on.
    /// </summary>
    internal sealed class AppReportSection : IReportSection
    {
        public string Name => "app";

        public void Collect(ReportSectionBuilder section)
        {
            var fields = new List<(string Key, string Value)>();
            SystemSnapshot.AppendApp(fields);
            section.AddRange(fields);

            section.Add("Session time", Application.isPlaying ? Duration(DiagnosticsClock.Elapsed) : "not playing");
            section.Add("Time scale", Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
            section.Add("Scenes", LoadedScenes());
            section.Add("Session log", SessionLog.CurrentPath != null ? Path.GetFileName(SessionLog.CurrentPath) : "none");
            section.Add("Log channels on", EnabledChannels());
        }

        private static string Duration(double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }

        private static string LoadedScenes()
        {
            var active = SceneManager.GetActiveScene();
            var names = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                string name = scene.name;
                if (scene == active) name += " (active)";
                if (!scene.isLoaded) name += " (loading)";
                names.Add(name);
            }
            return names.Count > 0 ? string.Join(", ", names) : "none";
        }

        private static string EnabledChannels()
        {
            var channels = new List<LogChannel>();
            LogChannelRegistry.CopyAll(channels);
            var on = new List<string>();
            foreach (var channel in channels)
                if (channel.Enabled) on.Add(channel.Name);
            on.Sort(StringComparer.Ordinal);
            return on.Count > 0 ? string.Join(", ", on) : "none";
        }
    }
}
