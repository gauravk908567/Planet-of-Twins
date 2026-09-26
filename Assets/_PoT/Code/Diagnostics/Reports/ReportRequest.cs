using System;
using System.Collections.Generic;

namespace PoT.Diagnostics
{
    /// <summary>What the player sent and what the game adds, for <see cref="ReportCollector.Collect"/>.</summary>
    public sealed class ReportRequest
    {
        /// <summary>"What happened?" as the player typed it. Never redacted.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>The quick category chip (Crash / Stuck / Visual / Controls / Other), game.md §27.8.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Optional email for a reply. Never redacted.</summary>
        public string Contact { get; set; } = string.Empty;

        /// <summary>The last gameplay frame as a JPG, or null (no screenshot for a crash report).</summary>
        public byte[] Screenshot { get; set; }

        /// <summary>The game's own sections, added after the package's (app, system, logs, crash).</summary>
        public IReadOnlyList<IReportSection> Sections { get; set; } = Array.Empty<IReportSection>();

        /// <summary>Include the last crash's evidence (<c>Logs/PendingCrash/</c>) when there is one.</summary>
        public bool IncludePendingCrash { get; set; } = true;
    }
}
