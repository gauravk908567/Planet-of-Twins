using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PoT.Diagnostics.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Planet of Twins' "Load this save" in the Report Inspector (game.md §27.6). A report's <c>save/pot_slot_N.json</c>
/// goes back into that slot on this PC, and the file already there is kept next to it as a <c>.bak</c>. "Play" then
/// starts from Bootstrap (build index 0), and Continue on that slot puts you where the player was.
/// Found by the Inspector through TypeCache.
/// </summary>
public sealed class PoTReportSaveHandler : IReportSaveHandler
{
    public bool CanLoad(string zipPath) => SlotOf(zipPath) >= 0;

    public string ButtonLabel(string zipPath) => $"Load into Slot {SlotOf(zipPath) + 1}";

    public string Explain(string zipPath)
    {
        int slot = SlotOf(zipPath);
        string target = SaveSystem.PathFor(slot);
        string current = SaveSystem.HasSave(slot)
            ? $"Your save in Slot {slot + 1} is kept next to it as a .bak copy."
            : $"Slot {slot + 1} is empty on this PC.";
        return $"This copies the report's save into Slot {slot + 1} on this PC:\n{target}\n\n{current}\n\n" +
               $"Then: Play from Bootstrap → Continue → Slot {slot + 1}.";
    }

    public string Load(string zipPath, byte[] contents)
    {
        int slot = SlotOf(zipPath);
        if (slot < 0) throw new ArgumentException($"'{zipPath}' is not a save slot file.");

        string target = SaveSystem.PathFor(slot);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string backup = null;
        if (File.Exists(target))
        {
            backup = target + ".before-report-" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".bak";
            File.Copy(target, backup);
        }
        File.WriteAllBytes(target, contents);

        var data = SaveSystem.Peek(slot);   // quiet: null = the game can't read it
        string result = data != null
            ? $"Slot {slot + 1} now holds the player's save: '{data.areaId}', saved {data.savedAtUtc} (UTC)."
            : $"Slot {slot + 1} holds the file, but the game can't read it (an older save version or a damaged " +
              "file), so it will show the slot as empty.";
        return backup != null ? $"{result}\n\nYour old save: {Path.GetFileName(backup)}" : result;
    }

    public void Play()
    {
        var boot = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
        if (boot == null)
        {
            Debug.LogError("[Report Inspector] Build Settings has no enabled scene to boot from (Bootstrap is index 0).");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(boot.path, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>The slot a report file belongs to (matched against the game's own save file names), or -1.</summary>
    private static int SlotOf(string zipPath)
    {
        if (zipPath == null || !zipPath.StartsWith(InspectedReport.SavePrefix, StringComparison.Ordinal)) return -1;
        string name = Path.GetFileName(zipPath);
        for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
            if (string.Equals(Path.GetFileName(SaveSystem.PathFor(slot)), name, StringComparison.OrdinalIgnoreCase))
                return slot;
        return -1;
    }
}
