using System.Collections.Generic;
using PoT.Diagnostics;

/// <summary>
/// <c>settings</c>: every settings-menu value (display, graphics API, quality, volumes, language, cursor), read
/// through the settings screen's own handlers. Both the Main Menu and Persistent have a settings screen.
/// </summary>
public sealed class SettingsReportSection : IReportSection
{
    public string Name => "settings";

    public void Collect(ReportSectionBuilder section)
    {
        var screen = SettingsScreenController.Instance;
        if (screen == null) { section.Add("Available", "no (no settings screen is loaded)"); return; }

        var fields = new List<(string Key, string Value)>();
        screen.DescribeForReport(fields);
        section.AddRange(fields);
    }
}
