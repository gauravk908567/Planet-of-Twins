using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Apply + persist for the AUDIO tab (master / music / sfx). Ported from the old
/// SettingsMenuController volume path: a linear 0-1 slider maps to decibels on the AudioMixer and is
/// mirrored to PlayerPrefs. Holds no UI reference — it works on plain float values, so it serves
/// whatever slider is bound to the id, wherever that slider ends up living.
/// </summary>
public sealed class AudioSettingsHandler : ISettingHandler
{
    private AudioMixer _mixer;

    private const string PrefMaster = "Vol_Master";
    private const string PrefMusic  = "Vol_Music";
    private const string PrefSfx    = "Vol_SFX";

    private const string ParamMaster = "MasterVolume";
    private const string ParamMusic  = "MusicVolume";
    private const string ParamSfx    = "SFXVolume";

    public void Initialize(SettingsBackendConfig config) => _mixer = config?.AudioMixer;

    public bool Owns(string id) =>
        id == SettingsCatalog.MasterVolume ||
        id == SettingsCatalog.MusicVolume  ||
        id == SettingsCatalog.SfxVolume;

    public IReadOnlyList<string> BuildDynamicOptions(string id) => null;
    public string GetReadout(string id) => null;

    // Audio settings are sliders only.
    public int GetInt(string id) => 0;
    public bool GetBool(string id) => false;
    public void SetInt(string id, int index) { }
    public void SetBool(string id, bool on) { }
    public void Invoke(string id) { }

    public float GetFloat(string id) => PlayerPrefs.GetFloat(PrefFor(id), 1f);

    public void SetFloat(string id, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefFor(id), value);
        PlayerPrefs.Save();
        ApplyToMixer(ParamFor(id), value);
    }

    /// <summary>Push saved volumes to the mixer at boot, even with no UI present (mirrors the old
    /// controller's LoadSavedSettings so audio is correct before the screen is ever opened).</summary>
    public void ApplySaved()
    {
        ApplyToMixer(ParamMaster, PlayerPrefs.GetFloat(PrefMaster, 1f));
        ApplyToMixer(ParamMusic,  PlayerPrefs.GetFloat(PrefMusic, 1f));
        ApplyToMixer(ParamSfx,    PlayerPrefs.GetFloat(PrefSfx, 1f));
    }

    private void ApplyToMixer(string param, float linear)
    {
        if (_mixer == null) return;
        // Linear 0-1 -> dB, floored at -80 dB (silence).
        float db = linear > 0.001f ? Mathf.Log10(linear) * 20f : -80f;
        _mixer.SetFloat(param, db);
    }

    private static string PrefFor(string id) =>
        id == SettingsCatalog.MusicVolume ? PrefMusic :
        id == SettingsCatalog.SfxVolume   ? PrefSfx   : PrefMaster;

    private static string ParamFor(string id) =>
        id == SettingsCatalog.MusicVolume ? ParamMusic :
        id == SettingsCatalog.SfxVolume   ? ParamSfx   : ParamMaster;
}
