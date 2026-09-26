using System.Collections.Generic;
using PoT.Diagnostics;
using UnityEngine.InputSystem;

/// <summary>
/// <c>input</c>: every connected device and the type the Input System gave it (a pad seen as "Joystick" has no
/// Gamepad bindings), which twin each player drives, and the couch pairing with each player's rebinds.
/// </summary>
public sealed class InputReportSection : IReportSection
{
    public string Name => "input";

    public void Collect(ReportSectionBuilder section)
    {
        var devices = InputSystem.devices;
        section.Add("Devices", devices.Count.ToString());
        for (int i = 0; i < devices.Count; i++)
            section.Add($"Device {i + 1}", $"{devices[i].displayName} ({devices[i].GetType().Name})");

        var roster = PlayerRoster.Instance;
        if (roster != null)
        {
            section.Add("P1 twin", TwinName(roster.For(PlayerSlot.One)));
            section.Add("P2 twin", TwinName(roster.For(PlayerSlot.Two)));
        }

        var couch = CouchDeviceManager.Instance;
        if (couch == null) { section.Add("Pairing", "not available (Persistent is not loaded)"); return; }
        var fields = new List<(string Key, string Value)>();
        couch.DescribeForReport(fields);
        section.AddRange(fields);
    }

    private static string TwinName(Player twin) => twin != null ? twin.name : "none";
}
