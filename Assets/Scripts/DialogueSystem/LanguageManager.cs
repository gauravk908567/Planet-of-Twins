using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Wraps Unity Localisation package.
/// Persistent singleton — survives scene loads.
/// Settings menu calls SetLanguage(). DialoguePlayer and UI subscribe to OnLanguageChanged.
///
/// SETUP:
///   Add to a persistent GO (DontDestroyOnLoad).
///   In Unity menu: Edit → Project Settings → Localisation
///     Add locales: English (default), Japanese, Chinese Simplified,
///     Chinese Traditional, Korean, Spanish, French, German, Arabic etc.
///   Create String Tables and Asset Tables in the Localisation window.
///   All DialogueLine.localisedText fields reference entries in those tables.
///
/// FALLBACK:
///   Unity Localisation automatically falls back to the default locale (English)
///   if a string is missing in the selected language. No extra code needed.
/// </summary>
public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }

    public event System.Action<Locale> OnLanguageChanged;

    /// <summary>Currently active locale.</summary>
    public Locale CurrentLocale => LocalizationSettings.SelectedLocale;

    /// <summary>All available locales in the project.</summary>
    public IReadOnlyList<Locale> AvailableLocales { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Persistent.unity residency = persistence (R3). DontDestroyOnLoad is banned.
    }

    private IEnumerator Start()
    {
        // Wait for localisation system to initialise
        yield return LocalizationSettings.InitializationOperation;

        AvailableLocales = LocalizationSettings.AvailableLocales.Locales;

        // Restore saved language preference
        string savedCode = PlayerPrefs.GetString("SelectedLocale", "");
        if (!string.IsNullOrEmpty(savedCode))
        {
            foreach (var locale in AvailableLocales)
            {
                if (locale.Identifier.Code == savedCode)
                {
                    LocalizationSettings.SelectedLocale = locale;
                    break;
                }
            }
        }

        // Subscribe to Unity Localisation's own change event
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    /// <summary>Called by SettingsMenuController when player picks a language.</summary>
    public void SetLanguage(Locale locale)
    {
        if (locale == null) return;
        LocalizationSettings.SelectedLocale = locale;
        PlayerPrefs.SetString("SelectedLocale", locale.Identifier.Code);
        PlayerPrefs.Save();
    }

    /// <summary>Set language by locale code string e.g. "en", "ja", "zh-Hans".</summary>
    public void SetLanguageByCode(string code)
    {
        foreach (var locale in AvailableLocales)
        {
            if (locale.Identifier.Code == code)
            {
                SetLanguage(locale);
                return;
            }
        }
        Debug.LogWarning($"[LanguageManager] Locale '{code}' not found — staying on {CurrentLocale}.");
    }

    private void OnLocaleChanged(Locale locale)
    {
        OnLanguageChanged?.Invoke(locale);
        Debug.Log($"[LanguageManager] Language changed to {locale.LocaleName}");
    }
}