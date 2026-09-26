namespace PoT.Diagnostics
{
    /// <summary>
    /// One part of a bug report. It adds fields to <c>report.json</c> (under <c>sections.&lt;Name&gt;</c>) and/or
    /// files to the zip. The package has its own sections (app, system, logs, crash); a game plugs in its state by
    /// passing more in <see cref="ReportRequest.Sections"/>.
    ///
    /// <see cref="Collect"/> runs on the main thread, so it may read Unity and scene state. It must never throw
    /// for a missing manager: add a field that says what was unavailable instead. Spec: game.md §27.3.
    /// </summary>
    public interface IReportSection
    {
        /// <summary>The key under <c>sections</c> in report.json: short, lower-case, unique in a report.</summary>
        string Name { get; }

        /// <summary>Adds this section's fields and files. Main thread.</summary>
        void Collect(ReportSectionBuilder section);
    }
}
