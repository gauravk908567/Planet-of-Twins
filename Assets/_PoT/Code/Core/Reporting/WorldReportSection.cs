using System.Collections.Generic;
using System.Globalization;
using PoT.Diagnostics;

/// <summary>
/// <c>world</c>: the active area, every loaded area, and where each tracked actor (both twins and the rescue
/// soul) is: its area and position. Enough to tell "stuck in a wall" from "the area never streamed in".
/// </summary>
public sealed class WorldReportSection : IReportSection
{
    public string Name => "world";

    public void Collect(ReportSectionBuilder section)
    {
        var flow = SceneFlowManager.Instance;
        if (flow == null) { section.Add("Streaming", "not running (Main Menu, or Persistent is not loaded)"); return; }

        section.Add("Active area", flow.ActiveLocation != null ? flow.ActiveLocation.name : "none");

        var loaded = new List<string>();
        foreach (var location in flow.LoadedLocations)
            if (location != null) loaded.Add(location.name);
        loaded.Sort(System.StringComparer.Ordinal);
        section.Add("Loaded areas", loaded.Count > 0 ? string.Join(", ", loaded) : "none");

        foreach (var pair in flow.ActorLocations)
        {
            if (pair.Key == null) continue;   // destroyed since it was tracked
            var p = pair.Key.transform.position;
            section.Add(pair.Key.name, string.Format(CultureInfo.InvariantCulture, "{0} at ({1:0.0}, {2:0.0}, {3:0.0})",
                pair.Value != null ? pair.Value.name : "no area", p.x, p.y, p.z));
        }
    }
}
