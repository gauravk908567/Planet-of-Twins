using System;

/// <summary>
/// Minimal, decoupled record of when the game last saved, for the Exit dialog's "Last saved: …"
/// line. The save/checkpoint code calls <see cref="MarkSaved"/> when it writes; the dialog reads
/// <see cref="Label"/>. Plain static state (no scene object) so it survives while the settings screen
/// is rebuilt or restyled. Returns "No save yet" until something records a save — saving is currently
/// inert, so this lights up for free once it is enabled and wired to call MarkSaved().
/// </summary>
public static class SaveTimeTracker
{
    public static DateTime? LastSaveLocal { get; private set; }

    public static void MarkSaved() => LastSaveLocal = DateTime.Now;

    public static string Label() =>
        LastSaveLocal.HasValue ? $"Last saved: {LastSaveLocal.Value:HH:mm}" : "No save yet";
}
