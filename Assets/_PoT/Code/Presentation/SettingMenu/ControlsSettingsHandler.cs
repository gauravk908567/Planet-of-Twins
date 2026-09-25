using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apply for the CONTROLS tab. Today it hosts a single Button — Restore Default Keybinds — ported
/// from PauseMenuController: clear every binding override via the shared input provider, then
/// refresh any on-screen prompt glyphs. Per-action rebinding rows (F6) will register more ids here
/// later, so the CONTROLS tab already has its handler in place.
/// </summary>
public sealed class ControlsSettingsHandler : ISettingHandler
{
    public void Initialize(SettingsBackendConfig config) { }

    public bool Owns(string id) => id == SettingsCatalog.RestoreKeybinds;

    public IReadOnlyList<string> BuildDynamicOptions(string id) => null;
    public string GetReadout(string id) => null;

    public int GetInt(string id) => 0;
    public float GetFloat(string id) => 0f;
    public bool GetBool(string id) => false;
    public void SetInt(string id, int index) { }
    public void SetFloat(string id, float value) { }
    public void SetBool(string id, bool on) { }

    public void Invoke(string id)
    {
        if (id != SettingsCatalog.RestoreKeybinds) return;

        PlayerInputRouter.SharedInput?.ResetBindingsToDefault();

        // InputPromptView is a scene-scoped non-singleton, so a sweep is the sanctioned lookup (R4).
        var prompts = Object.FindObjectsByType<InputPromptView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in prompts) p.Refresh();
    }
}
