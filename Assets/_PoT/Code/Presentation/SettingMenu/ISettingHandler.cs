using System.Collections.Generic;

/// <summary>
/// Owns the apply + persist logic for a group of settings, keyed by <see cref="SettingsCatalog"/>
/// Id. The SettingsScreenController routes a changed control's value to the handler that
/// <see cref="Owns"/> its Id, and reads back values to initialise controls when the screen opens.
///
/// A handler holds NO reference to any UI control — it operates on plain values (dropdown index,
/// slider float, toggle bool) — so the same handler serves whatever control is bound to the Id,
/// before or after a restructure or restyle. Handlers are plain C# objects; the controller injects
/// asset dependencies via <see cref="Initialize"/>.
/// </summary>
public interface ISettingHandler
{
    /// <summary>True if this handler is responsible for the given setting Id.</summary>
    bool Owns(string id);

    /// <summary>Inject asset dependencies (mixer, URP asset, renderer data, …) before first use.</summary>
    void Initialize(SettingsBackendConfig config);

    /// <summary>Dropdown option labels built at runtime (language, resolution). Return null to use
    /// the catalog's static <see cref="SettingDefinition.Options"/>.</summary>
    IReadOnlyList<string> BuildDynamicOptions(string id);

    /// <summary>Optional text for a row's value readout label (render-scale "1.00", graphics-API
    /// "Current: DirectX 11"). Return null when the row has no readout.</summary>
    string GetReadout(string id);

    // ── Read current effective value (to initialise a control on open) ──
    int GetInt(string id);      // dropdown selected index
    float GetFloat(string id);  // slider value
    bool GetBool(string id);    // toggle state

    // ── Apply + persist a change coming from the control ──
    void SetInt(string id, int index);
    void SetFloat(string id, float value);
    void SetBool(string id, bool on);

    /// <summary>Fire a Button-type row (e.g. Restore Default Keybinds).</summary>
    void Invoke(string id);
}
