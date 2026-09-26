using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace PoT.Diagnostics
{
    /// <summary>
    /// The trail of key events a bug report carries instead of player-written repro steps. A ring buffer of the
    /// last <see cref="Capacity"/> crumbs; each crumb is also written to the session log, so the trail survives
    /// a native crash. Record events at human rate (area loaded, checkpoint, death, pause…), never per frame.
    /// Scene loads, focus changes and low memory are recorded automatically (DiagnosticsRuntime). Thread-safe.
    /// </summary>
    public static class Breadcrumbs
    {
        public const int Capacity = 200;

        private static readonly Breadcrumb[] Ring = new Breadcrumb[Capacity];
        private static readonly object Gate = new object();
        private static int _next;
        private static int _count;

        public static void Add(string category, string text)
        {
            string scene = DiagnosticsClock.IsMainThread ? SceneManager.GetActiveScene().name : "?";
            var crumb = new Breadcrumb(DiagnosticsClock.Elapsed, DiagnosticsClock.Frame, category, text, scene);
            lock (Gate)
            {
                Ring[_next] = crumb;
                _next = (_next + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
            SessionLog.WriteCrumb(crumb);
        }

        /// <summary>Copies the trail into <paramref name="into"/> (cleared first), oldest first.</summary>
        public static void CopyTo(List<Breadcrumb> into)
        {
            into.Clear();
            lock (Gate)
            {
                int start = (_next - _count + Capacity) % Capacity;
                for (int i = 0; i < _count; i++)
                    into.Add(Ring[(start + i) % Capacity]);
            }
        }

        internal static void Clear()
        {
            lock (Gate)
            {
                System.Array.Clear(Ring, 0, Capacity);
                _next = 0;
                _count = 0;
            }
        }
    }
}
