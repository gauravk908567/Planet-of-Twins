using System.Collections.Generic;

namespace PoT.Diagnostics
{
    /// <summary><c>system</c>: OS, CPU, RAM, GPU + VRAM, graphics API, display, window mode, quality level.</summary>
    internal sealed class SystemReportSection : IReportSection
    {
        public string Name => "system";

        public void Collect(ReportSectionBuilder section)
        {
            var fields = new List<(string Key, string Value)>();
            SystemSnapshot.AppendSystem(fields);
            section.AddRange(fields);
        }
    }
}
