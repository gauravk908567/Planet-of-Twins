using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The GAMEPLAY tab's Support rows (game.md §27): the "Report a Problem" button opens this scene's report screen
/// over the settings screen, and the "Detailed Logging" toggle is <see cref="DetailedLogging"/>.
/// </summary>
public sealed class SupportSettingsHandler : ISettingHandler
{
    public void Initialize(SettingsBackendConfig config) { }

    public bool Owns(string id) =>
        id == SettingsCatalog.ReportProblem ||
        id == SettingsCatalog.DetailedLogging;

    public IReadOnlyList<string> BuildDynamicOptions(string id) => null;
    public string GetReadout(string id) => null;

    public int GetInt(string id) => 0;
    public float GetFloat(string id) => 0f;
    public void SetInt(string id, int index) { }
    public void SetFloat(string id, float value) { }

    public bool GetBool(string id) => id == SettingsCatalog.DetailedLogging && DetailedLogging.Enabled;

    public void SetBool(string id, bool on)
    {
        if (id == SettingsCatalog.DetailedLogging) DetailedLogging.Set(on);
    }

    public void Invoke(string id)
    {
        if (id != SettingsCatalog.ReportProblem) return;
        // Same-scene screen (FrontEnd or Persistent each hold one copy of the prefab).
        var screen = ReportProblemScreen.Instance;
        if (screen == null)
        {
            Debug.LogError("[SupportSettingsHandler] No ReportProblemScreen in this scene; Report a Problem does nothing. " +
                           "Place the ReportProblemScreen prefab next to the UnifiedSettings screen.");
            return;
        }
        screen.Open();
    }
}
