using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settings panel inside the pause menu.
/// Handles: language, resolution, window mode, cursor, master/music/sfx volume.
///
/// SETUP:
///   Add to SettingsPanel GO (child of PauseRoot).
///   Wire all refs in Inspector.
///   AudioMixer asset needed for volume sliders.
///
/// HIERARCHY:
///   SettingsPanel
///     ├── LanguageDropdown    TMP_Dropdown
///     ├── ResolutionDropdown  TMP_Dropdown
///     ├── WindowModeDropdown  TMP_Dropdown
///     ├── CursorToggle        Toggle
///     ├── MasterVolumeSlider  Slider
///     ├── MusicVolumeSlider   Slider
///     ├── SFXVolumeSlider     Slider
///     ├── ApplyButton         Button
///     └── BackButton          Button → PauseMenuController.CloseSettings()
/// </summary>
public class SettingsMenuController : MonoBehaviour
{
    [Header("Language")]
    [SerializeField] private TMP_Dropdown _languageDropdown;

    [Header("Display")]
    [SerializeField] private TMP_Dropdown _resolutionDropdown;
    [SerializeField] private TMP_Dropdown _windowModeDropdown;
    [SerializeField] private Toggle _cursorToggle;

    [Header("Audio")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("Buttons")]
    [SerializeField] private Button _applyButton;
    [SerializeField] private Button _backButton;

    private Resolution[] _resolutions;
    private IReadOnlyList<Locale> _locales;

    private void Start()
    {
        StartCoroutine(InitialiseAfterLocalisation());
    }

    private IEnumerator InitialiseAfterLocalisation()
    {
        // Wait until LanguageManager is ready
        while (LanguageManager.Instance == null ||
               LanguageManager.Instance.AvailableLocales == null)
            yield return null;

        BuildLanguageDropdown();
        BuildResolutionDropdown();
        BuildWindowModeDropdown();
        LoadSavedSettings();

        _applyButton?.onClick.AddListener(ApplySettings);
        _backButton?.onClick.AddListener(() =>
            PauseMenuController.Instance?.CloseSettings());
    }

    // ── Language ──────────────────────────────────────────────
    private void BuildLanguageDropdown()
    {
        _locales = LanguageManager.Instance.AvailableLocales;
        if (_languageDropdown == null || _locales == null) return;

        var options = new List<TMP_Dropdown.OptionData>();
        int currentIndex = 0;

        for (int i = 0; i < _locales.Count; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(_locales[i].LocaleName));
            if (_locales[i] == LanguageManager.Instance.CurrentLocale)
                currentIndex = i;
        }

        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(options);
        _languageDropdown.value = currentIndex;
        _languageDropdown.RefreshShownValue();
    }

    // ── Resolution ────────────────────────────────────────────
    private void BuildResolutionDropdown()
    {
        if (_resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        var options = new List<TMP_Dropdown.OptionData>();
        int current = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            options.Add(new TMP_Dropdown.OptionData($"{r.width} × {r.height} @ {r.refreshRateRatio.value:F0}Hz"));
            if (r.width == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
                current = i;
        }

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);
        _resolutionDropdown.value = current;
        _resolutionDropdown.RefreshShownValue();
    }

    private void BuildWindowModeDropdown()
    {
        if (_windowModeDropdown == null) return;
        _windowModeDropdown.ClearOptions();
        _windowModeDropdown.AddOptions(new List<string>
        {
            "Fullscreen", "Windowed Fullscreen", "Windowed"
        });
        _windowModeDropdown.value = Screen.fullScreenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => 0,
            FullScreenMode.FullScreenWindow => 1,
            FullScreenMode.Windowed => 2,
            _ => 1
        };
    }

    // ── Load saved ────────────────────────────────────────────
    private void LoadSavedSettings()
    {
        if (_masterSlider) _masterSlider.value = PlayerPrefs.GetFloat("Vol_Master", 1f);
        if (_musicSlider) _musicSlider.value = PlayerPrefs.GetFloat("Vol_Music", 1f);
        if (_sfxSlider) _sfxSlider.value = PlayerPrefs.GetFloat("Vol_SFX", 1f);
        if (_cursorToggle) _cursorToggle.isOn = PlayerPrefs.GetInt("CursorVisible", 0) == 1;

        ApplyVolume("MasterVolume", _masterSlider?.value ?? 1f);
        ApplyVolume("MusicVolume", _musicSlider?.value ?? 1f);
        ApplyVolume("SFXVolume", _sfxSlider?.value ?? 1f);
    }

    // ── Apply ─────────────────────────────────────────────────
    private void ApplySettings()
    {
        // Language
        if (_languageDropdown != null && _locales != null &&
            _languageDropdown.value < _locales.Count)
            LanguageManager.Instance?.SetLanguage(_locales[_languageDropdown.value]);

        // Resolution
        if (_resolutionDropdown != null && _resolutions != null &&
            _resolutionDropdown.value < _resolutions.Length)
        {
            var r = _resolutions[_resolutionDropdown.value];
            var mode = _windowModeDropdown?.value switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.FullScreenWindow,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
            Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
        }

        // Cursor
        if (_cursorToggle != null)
        {
            Cursor.visible = _cursorToggle.isOn;
            PlayerPrefs.SetInt("CursorVisible", _cursorToggle.isOn ? 1 : 0);
        }

        // Volume
        float master = _masterSlider?.value ?? 1f;
        float music = _musicSlider?.value ?? 1f;
        float sfx = _sfxSlider?.value ?? 1f;

        ApplyVolume("MasterVolume", master);
        ApplyVolume("MusicVolume", music);
        ApplyVolume("SFXVolume", sfx);

        PlayerPrefs.SetFloat("Vol_Master", master);
        PlayerPrefs.SetFloat("Vol_Music", music);
        PlayerPrefs.SetFloat("Vol_SFX", sfx);
        PlayerPrefs.Save();

        PauseMenuController.Instance?.CloseSettings();
    }

    private void ApplyVolume(string param, float linear)
    {
        if (_audioMixer == null) return;
        // Convert linear 0-1 to decibels. Floor at -80dB (silence).
        float db = linear > 0.001f ? Mathf.Log10(linear) * 20f : -80f;
        _audioMixer.SetFloat(param, db);
    }
}