using System;
using System.Collections;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Sends a written report zip to the team (game.md §27.3). The Apps Script relay is the first one; a bug tracker
    /// (e.g. GlitchTip) can replace it behind this seam.
    ///
    /// <see cref="Send"/> is a coroutine, because web requests run on the main thread: the caller runs it with
    /// <c>StartCoroutine</c>. It never throws and calls <c>done</c> exactly once. It uses no scaled time, so it runs
    /// while the game is paused.
    /// </summary>
    public interface IReportUploader
    {
        /// <summary>For logs: which uploader sent (or failed to send) a report.</summary>
        string Name { get; }

        IEnumerator Send(ReportPackage package, string zipPath, Action<ReportUploadResult> done);
    }
}
