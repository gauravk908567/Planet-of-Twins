using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Apply + persist for language, cursor, resolution and window mode — ported from the old
/// SettingsMenuController. Resolution and window mode are coupled (Screen.SetResolution needs both),
/// so this handler caches both selected indices and re-applies the pair whenever either changes.
/// Deferred settings (resolution/window) are handed here by the controller only after the user
/// confirms; the handler itself just applies plain values and holds no UI reference.
/// </summary>
public sealed class DisplaySettingsHandler : ISettingHandler
{
    private Resolution[] _resolutions;
    private IReadOnlyList<Locale> _locales;
    private int _resIndex;
    private int _windowIndex = 1; // default Windowed Fullscreen

    private const string PrefCursor = "CursorVisible";

    public void Initialize(SettingsBackendConfig config)
    {
        _resolutions = Screen.resolutions;
        _resIndex = CurrentResolutionIndex();
        _windowIndex = CurrentWindowIndex();
    }

    public bool Owns(string id) =>
        id == SettingsCatalog.Language ||
        id == SettingsCatalog.Cursor ||
        id == SettingsCatalog.Resolution ||
        id == SettingsCatalog.WindowMode;

    public IReadOnlyList<string> BuildDynamicOptions(string id)
    {
        if (id == SettingsCatalog.Language) return LanguageOptions();
        if (id == SettingsCatalog.Resolution) return ResolutionOptions();
        return null; // window mode uses the catalog's static options
    }

    public string GetReadout(string id) => null;

    public int GetInt(string id)
    {
        if (id == SettingsCatalog.Language) return CurrentLanguageIndex();
        if (id == SettingsCatalog.Resolution) return _resIndex;
        if (id == SettingsCatalog.WindowMode) return _windowIndex;
        return 0;
    }

    public void SetInt(string id, int index)
    {
        if (id == SettingsCatalog.Language) { ApplyLanguage(index); return; }
        if (id == SettingsCatalog.Resolution) { _resIndex = index; ApplyDisplay(); return; }
        if (id == SettingsCatalog.WindowMode) { _windowIndex = index; ApplyDisplay(); return; }
    }

    public bool GetBool(string id) =>
        id == SettingsCatalog.Cursor && PlayerPrefs.GetInt(PrefCursor, 0) == 1;

    public void SetBool(string id, bool on)
    {
        if (id != SettingsCatalog.Cursor) return;
        Cursor.visible = on;
        PlayerPrefs.SetInt(PrefCursor, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public float GetFloat(string id) => 0f;
    public void SetFloat(string id, float value) { }
    public void Invoke(string id) { }

    // ── Language ──────────────────────────────────────────────
    private IReadOnlyList<string> LanguageOptions()
    {
        _locales = LanguageManager.Instance != null ? LanguageManager.Instance.AvailableLocales : null;
        if (_locales == null) return new List<string>();
        var names = new List<string>(_locales.Count);
        foreach (var l in _locales) names.Add(l.LocaleName);
        return names;
    }

    private int CurrentLanguageIndex()
    {
        if (_locales == null) LanguageOptions();
        if (_locales == null || LanguageManager.Instance == null) return 0;
        var current = LanguageManager.Instance.CurrentLocale;
        for (int i = 0; i < _locales.Count; i++)
            if (_locales[i] == current) return i;
        return 0;
    }

    private void ApplyLanguage(int index)
    {
        if (_locales == null) LanguageOptions();
        if (_locales == null || index < 0 || index >= _locales.Count) return;
        LanguageManager.Instance?.SetLanguage(_locales[index]);
    }

    // ── Resolution / window mode ──────────────────────────────
    private IReadOnlyList<string> ResolutionOptions()
    {
        if (_resolutions == null || _resolutions.Length == 0) _resolutions = Screen.resolutions;
        var opts = new List<string>(_resolutions.Length);
        foreach (var r in _resolutions)
            opts.Add($"{r.width} × {r.height} @ {r.refreshRateRatio.value:F0}Hz");
        return opts;
    }

    private int CurrentResolutionIndex()
    {
        var res = Screen.resolutions;
        for (int i = 0; i < res.Length; i++)
            if (res[i].width == Screen.currentResolution.width &&
                res[i].height == Screen.currentResolution.height) return i;
        return Mathf.Max(0, res.Length - 1);
    }

    private int CurrentWindowIndex() => Screen.fullScreenMode switch
    {
        FullScreenMode.ExclusiveFullScreen => 0,
        FullScreenMode.FullScreenWindow => 1,
        FullScreenMode.Windowed => 2,
        _ => 1
    };

    private void ApplyDisplay()
    {
        if (_resolutions == null || _resolutions.Length == 0) return;
        int ri = Mathf.Clamp(_resIndex, 0, _resolutions.Length - 1);
        var r = _resolutions[ri];
        var mode = _windowIndex switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            2 => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow
        };
        Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
    }
}
