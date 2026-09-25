using System;

/// <summary>
/// Which top-bar tab a setting belongs to. Resume and Exit are bar BUTTONS (far-left / far-right),
/// not tabs — they are handled by the screen controller, not the catalog.
/// </summary>
public enum SettingTab { Gameplay, Video, Audio, Controls }

/// <summary>The kind of control a setting row presents.</summary>
public enum SettingControlType { Dropdown, Slider, Toggle, Button }

/// <summary>
/// Live     = apply the instant the control changes (most settings).
/// Deferred = disruptive (resolution, window mode, graphics API); apply only on explicit confirm.
/// </summary>
public enum SettingApplyMode { Live, Deferred }

/// <summary>
/// Pure DATA for one setting: the single source of truth for WHAT it is and WHERE it sits
/// (tab + section + order). Deliberately holds NO GameObject reference. At runtime the visual row
/// is bound to this by <see cref="Id"/>, and the apply/persist logic is keyed by the same Id, so
/// re-homing a setting (a different tab/section), rebuilding the screen, or restyling it can never
/// break the wiring. See <c>SettingsCatalog</c> for the ordered list.
/// </summary>
public sealed class SettingDefinition
{
    /// <summary>Stable binding key, e.g. "video.resolution". Persisted handlers key off this —
    /// do not rename casually.</summary>
    public string Id { get; }

    public SettingTab Tab { get; }

    /// <summary>Sub-header this row groups under, e.g. "Display" / "Quality" / "Volume".</summary>
    public string Section { get; }

    /// <summary>Human-readable row label (plain string for now; localization key wiring is a
    /// later pass and rides on the same Id).</summary>
    public string Label { get; }

    public SettingControlType Type { get; }

    /// <summary>Fixed dropdown labels, or null when the handler fills them at runtime
    /// (language, resolution).</summary>
    public string[] Options { get; }

    /// <summary>Slider range (ignored for non-slider types).</summary>
    public float Min { get; }
    public float Max { get; }

    public SettingApplyMode ApplyMode { get; }

    public SettingDefinition(string id, SettingTab tab, string section, string label,
        SettingControlType type, string[] options = null, float min = 0f, float max = 1f,
        SettingApplyMode applyMode = SettingApplyMode.Live)
    {
        Id = id;
        Tab = tab;
        Section = section;
        Label = label;
        Type = type;
        Options = options;
        Min = min;
        Max = max;
        ApplyMode = applyMode;
    }
}
