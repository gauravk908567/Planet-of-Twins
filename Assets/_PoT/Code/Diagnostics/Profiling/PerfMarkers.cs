using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Every profiler marker the game puts on a hot path, by name. A hot path creates its marker here instead of
    /// <c>new ProfilerMarker(...)</c>, so a perf overlay can list every marker without keeping its own list of names.
    /// Same idea as <see cref="LogChannelRegistry"/>: the game names the markers, this package only stores them.
    ///
    /// Use: <c>static readonly ProfilerMarker PerfTick = PerfMarkers.Create("PoT.Spawner.Tick");</c>, then
    /// <c>using (PerfTick.Auto()) { … }</c>. A marker must begin and end in the same frame, so its scope never
    /// contains a <c>yield</c>. A name shows up in <see cref="CopyNames"/> once its class has been used.
    /// Cost: a few nanoseconds per scope where the profiler is compiled in (Editor, development builds); release
    /// builds leave the markers inactive.
    /// </summary>
    public static class PerfMarkers
    {
        private static readonly List<string> Names = new List<string>();
        private static readonly object Gate = new object();

        /// <summary>The marker called <paramref name="name"/>, recorded so an overlay can find it.</summary>
        public static ProfilerMarker Create(string name)
        {
            Watch(name);
            return new ProfilerMarker(name);
        }

        /// <summary>Lists a marker declared somewhere that can't reference this package (e.g. a sealed framework
        /// that creates its own <c>new ProfilerMarker(name)</c>). Unity keeps one marker per name, so the overlay
        /// records the same marker.</summary>
        public static void Watch(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A profiler marker needs a name.", nameof(name));
            lock (Gate)
            {
                if (!Names.Contains(name)) Names.Add(name);
            }
        }

        /// <summary>How many markers exist so far (an overlay compares it to spot new ones).</summary>
        public static int Count
        {
            get { lock (Gate) return Names.Count; }
        }

        /// <summary>Copies every marker name into <paramref name="into"/> (cleared first), in creation order.</summary>
        public static void CopyNames(List<string> into)
        {
            into.Clear();
            lock (Gate) into.AddRange(Names);
        }
    }
}
