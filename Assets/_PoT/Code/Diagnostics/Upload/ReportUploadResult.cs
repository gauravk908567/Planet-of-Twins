using System.Collections.Generic;

namespace PoT.Diagnostics
{
    /// <summary>What happened to one upload: its <see cref="Status"/>, a message a player can read, and the
    /// step-by-step trace (for the session log and the relay test tool).</summary>
    public sealed class ReportUploadResult
    {
        public enum Status
        {
            /// <summary>The server confirmed it has the report.</summary>
            Sent,
            /// <summary>The server took the report, but its answer was lost (seen once: Apps Script ran the script
            /// and emailed the report, then the answer's address returned 404). Most likely it arrived.</summary>
            Unconfirmed,
            /// <summary>It didn't get through (offline, refused, too big…). The zip stays on the PC.</summary>
            NotSent,
        }

        public Status Outcome { get; }

        public bool Sent => Outcome == Status.Sent;

        /// <summary>Why it wasn't sent or confirmed, in words for the player ("this PC looks offline"); when sent,
        /// the server's note.</summary>
        public string Message { get; }

        public IReadOnlyList<string> Trace { get; }

        private ReportUploadResult(Status outcome, string message, List<string> trace)
        {
            Outcome = outcome;
            Message = message ?? string.Empty;
            Trace = trace ?? new List<string>();
        }

        public static ReportUploadResult Success(string serverMessage, List<string> trace) =>
            new ReportUploadResult(Status.Sent, serverMessage, trace);

        public static ReportUploadResult Unconfirmed(string reason, List<string> trace) =>
            new ReportUploadResult(Status.Unconfirmed, reason, trace);

        public static ReportUploadResult Failure(string reason, List<string> trace) =>
            new ReportUploadResult(Status.NotSent, reason, trace);

        public string TraceText => string.Join("\n", Trace);
    }
}
