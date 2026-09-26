using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Thread-safe time source for log lines and breadcrumbs. Unity's <c>Time</c> and <c>SceneManager</c> are
    /// main-thread only, but <c>Application.logMessageReceivedThreaded</c> can fire on any thread, so everything
    /// that stamps a line goes through here.
    /// </summary>
    internal static class DiagnosticsClock
    {
        private static readonly Stopwatch Watch = new Stopwatch();
        private static int _mainThreadId = -1;

        /// <summary>Called once per session on the main thread (DiagnosticsRuntime).</summary>
        internal static void Start()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Watch.Restart();
        }

        internal static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>Real seconds since the session started (unscaled wall time: pause/slow-mo don't affect it).</summary>
        internal static double Elapsed => Watch.Elapsed.TotalSeconds;

        /// <summary><c>Time.frameCount</c> on the main thread, -1 on any other thread.</summary>
        internal static int Frame => IsMainThread ? Time.frameCount : -1;
    }
}
