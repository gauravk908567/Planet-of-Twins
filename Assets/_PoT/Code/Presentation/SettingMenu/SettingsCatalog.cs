using System.Collections.Generic;

/// <summary>
/// The ordered catalog of every setting on the unified pause/settings screen. STRUCTURE lives here
/// (tab, section, order); the screen builder reads it to generate rows, and the runtime binds each
/// control to its apply/persist handler by <see cref="SettingDefinition.Id"/>. Adding a setting is
/// one definition line — its row, binding and apply all follow from the Id. Nothing references a
/// setting by GameObject, so structure or visual changes cannot break function.
///
/// This is deliberately the ONLY place that declares what the settings screen contains; the
/// Valorant/OW-style tab layout (Resume | GAMEPLAY VIDEO AUDIO CONTROLS | Exit) is a projection of
/// this list, grouped by <see cref="SettingDefinition.Tab"/> then <see cref="SettingDefinition.Section"/>.
/// </summary>
public static class SettingsCatalog
{
    // ── Stable IDs (the binding key; persisted handlers key off these) ──────────────
    // Gameplay
    public const string Language         = "gameplay.language";
    public const string Cursor           = "gameplay.cursor";
    // Video · Display
    public const string WindowMode       = "video.windowmode";
    public const string Resolution       = "video.resolution";
    public const string VSync            = "video.vsync";
    public const string FpsCap           = "video.fpscap";
    public const string GraphicsApi      = "video.api";
    // Video · Quality
    public const string QualityPreset    = "video.preset";
    public const string RenderScale      = "video.renderscale";
    public const string Texture          = "video.texture";
    public const string Shadow           = "video.shadow";
    public const string AntiAliasing     = "video.aa";
    public const string AmbientOcclusion = "video.ao";
    public const string Fog              = "video.fog";
    public const string SunShafts        = "video.shafts";
    public const string Terrain          = "video.terrain";
    // Audio · Volume
    public const string MasterVolume     = "audio.master";
    public const string MusicVolume      = "audio.music";
    public const string SfxVolume        = "audio.sfx";
    // Controls · Keybinds (foundation for F6 rebinding + current-keybind display)
    public const string RestoreKeybinds  = "controls.restoredefaults";

    // ── Section (sub-header) names ──────────────────────────────────────────────────
    private const string General  = "General";
    private const string Display  = "Display";
    private const string Quality  = "Quality";
    private const string Volume   = "Volume";
    private const string Keybinds = "Keybinds";

    public static IReadOnlyList<SettingDefinition> Definitions => _defs;

    private static readonly List<SettingDefinition> _defs = new List<SettingDefinition>
    {
        // ── GAMEPLAY · General ──────────────────────────────────────────────────────
        new SettingDefinition(Language, SettingTab.Gameplay, General, "Language",
            SettingControlType.Dropdown),                       // options filled at runtime
        new SettingDefinition(Cursor, SettingTab.Gameplay, General, "Show Cursor",
            SettingControlType.Toggle),

        // ── VIDEO · Display ─────────────────────────────────────────────────────────
        new SettingDefinition(WindowMode, SettingTab.Video, Display, "Window Mode",
            SettingControlType.Dropdown,
            new[] { "Fullscreen", "Windowed Fullscreen", "Windowed" },
            applyMode: SettingApplyMode.Deferred),
        new SettingDefinition(Resolution, SettingTab.Video, Display, "Resolution",
            SettingControlType.Dropdown, applyMode: SettingApplyMode.Deferred), // options at runtime
        new SettingDefinition(VSync, SettingTab.Video, Display, "V-Sync",
            SettingControlType.Toggle),
        new SettingDefinition(FpsCap, SettingTab.Video, Display, "Max FPS",
            SettingControlType.Dropdown, new[] { "Off", "60", "120", "144" }),
        new SettingDefinition(GraphicsApi, SettingTab.Video, Display, "Graphics API",
            SettingControlType.Dropdown, new[] { "Auto", "DirectX 11", "DirectX 12" },
            applyMode: SettingApplyMode.Deferred),

        // ── VIDEO · Quality ─────────────────────────────────────────────────────────
        new SettingDefinition(QualityPreset, SettingTab.Video, Quality, "Quality Preset",
            SettingControlType.Dropdown, new[] { "Low", "Medium", "High", "Custom" }),
        new SettingDefinition(RenderScale, SettingTab.Video, Quality, "Render Scale",
            SettingControlType.Slider, min: 0.7f, max: 1.0f),
        new SettingDefinition(Texture, SettingTab.Video, Quality, "Texture Quality",
            SettingControlType.Dropdown, new[] { "High", "Medium", "Low" }),
        new SettingDefinition(Shadow, SettingTab.Video, Quality, "Shadow Quality",
            SettingControlType.Dropdown, new[] { "Low", "Medium", "High" }),
        new SettingDefinition(AntiAliasing, SettingTab.Video, Quality, "Anti-Aliasing",
            SettingControlType.Dropdown, new[] { "Off", "SMAA" }),
        new SettingDefinition(AmbientOcclusion, SettingTab.Video, Quality, "Ambient Occlusion",
            SettingControlType.Toggle),
        new SettingDefinition(Fog, SettingTab.Video, Quality, "Volumetric Fog",
            SettingControlType.Dropdown, new[] { "Off", "Low", "High" }),
        new SettingDefinition(SunShafts, SettingTab.Video, Quality, "Sun Shafts",
            SettingControlType.Toggle),
        new SettingDefinition(Terrain, SettingTab.Video, Quality, "Terrain Quality",
            SettingControlType.Dropdown, new[] { "Low", "Medium", "High" }),

        // ── AUDIO · Volume ──────────────────────────────────────────────────────────
        new SettingDefinition(MasterVolume, SettingTab.Audio, Volume, "Master Volume",
            SettingControlType.Slider, min: 0f, max: 1f),
        new SettingDefinition(MusicVolume, SettingTab.Audio, Volume, "Music Volume",
            SettingControlType.Slider, min: 0f, max: 1f),
        new SettingDefinition(SfxVolume, SettingTab.Audio, Volume, "SFX Volume",
            SettingControlType.Slider, min: 0f, max: 1f),

        // ── CONTROLS · Keybinds (foundation for F6 rebinding) ───────────────────────
        new SettingDefinition(RestoreKeybinds, SettingTab.Controls, Keybinds,
            "Restore Default Keybinds", SettingControlType.Button),
    };
}
