using System.IO;
using PoT.Diagnostics;

/// <summary>
/// <c>save</c>: the active slot and its file (<c>save/pot_slot_N.json</c>), so the Report Inspector can load the
/// player's situation. From the Main Menu no slot is active yet, so every save on disk goes in: they are a few
/// KB each, and "my save won't load" is a likely report from there.
/// </summary>
public sealed class SaveReportSection : IReportSection
{
    public string Name => "save";

    public void Collect(ReportSectionBuilder section)
    {
        var save = SaveService.Instance;
        if (save == null)
        {
            section.Add("Active slot", "none (Main Menu: Persistent is not loaded)");
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
                if (SaveSystem.HasSave(slot)) AddSlot(section, slot);
            return;
        }

        section.Add("Active slot", save.ActiveSlot == SaveService.NoSlot
            ? "none (dev boot or TestLab: nothing is saved)" : save.ActiveSlot.ToString());
        section.Add("Saving enabled", save.SavingEnabled ? "yes" : "no");
        section.Add("Started from", save.IsResumingSave ? "Continue" : "New Game");
        if (SaveSystem.IsValidSlot(save.ActiveSlot)) AddSlot(section, save.ActiveSlot);
    }

    private static void AddSlot(ReportSectionBuilder section, int slot)
    {
        string path = SaveSystem.PathFor(slot);
        if (!SaveSystem.HasSave(slot)) { section.Add($"Slot {slot}", "no file yet"); return; }

        var data = SaveSystem.Peek(slot);   // quiet; null = unreadable or an old version
        section.Add($"Slot {slot}", data != null
            ? $"saved {data.savedAtUtc} (UTC) in '{data.areaId}', version {data.version}"
            : "file present but not loadable (corrupt or an old version)");
        section.AddTextFile("save/" + Path.GetFileName(path), path);
    }
}
