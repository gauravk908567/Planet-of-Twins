using PoT.Diagnostics;

/// <summary>
/// <c>dev</c>: the DevConfig flags in effect (always off in a release build). A report from a build with the
/// Trainer or the tutorial skip on explains otherwise odd progress. Which log channels are on is in <c>app</c>.
/// </summary>
public sealed class DevReportSection : IReportSection
{
    public string Name => "dev";

    public void Collect(ReportSectionBuilder section)
    {
        section.Add("Trainer", DevConfig.Trainer ? "on" : "off");
        section.Add("Skip tutorial", DevConfig.SkipTutorial ? "on" : "off");
    }
}
