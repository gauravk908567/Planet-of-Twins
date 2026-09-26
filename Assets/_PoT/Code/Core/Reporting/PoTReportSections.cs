using System.Collections.Generic;
using PoT.Diagnostics;

/// <summary>
/// Planet of Twins' part of a bug report (game.md §27.3), added after the package's own sections (app, system,
/// logs, crash): settings, input, save, world and dev. Pass <see cref="All"/> as <c>ReportRequest.Sections</c>.
/// Each section reads its managers at collect time and says so in a field when one isn't loaded (the Main Menu
/// runs before Persistent exists), so a report can be built from any scene.
/// </summary>
public static class PoTReportSections
{
    public static readonly IReadOnlyList<IReportSection> All = new IReportSection[]
    {
        new SettingsReportSection(),
        new InputReportSection(),
        new SaveReportSection(),
        new WorldReportSection(),
        new DevReportSection(),
    };
}
