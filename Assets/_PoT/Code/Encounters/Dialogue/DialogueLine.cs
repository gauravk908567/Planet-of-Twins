using UnityEngine;
using UnityEngine.Localization;

public enum DialogueSpeaker { Lyra, Kai }

/// <summary>
/// A single line in a DialogueSequenceSO.
/// playSimultaneous = true means this line plays at the same time
/// as the PREVIOUS line, not after it. Use for two-character exchanges
/// that happen simultaneously (e.g. Lyra says something, Kai reacts at the same time).
///
/// No pipe separators, no hidden reaction fields.
/// Each line is explicit — one speaker, one text, one audio.
///
/// AUDIO SYNC:
///   wordTimings[] empty → full text shown at once (Option B, default for VS)
///   wordTimings[] populated → words highlight as audio plays (Option C, add later)
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("Who speaks this line.")]
    public DialogueSpeaker speaker = DialogueSpeaker.Lyra;

    [Tooltip("If true, this line plays at the same time as the previous line. " +
             "Use for simultaneous reactions — Lyra speaks, Kai reacts in parallel. " +
             "The previous line's duration governs both.")]
    public bool playSimultaneous = false;

    [Tooltip("Localised text for this line.")]
    public LocalizedString localisedText;

    [Tooltip("Localised audio clip. Leave empty until recordings arrive.")]
    public LocalizedAudioClip audioClip;

    [Tooltip("Word timing offsets in seconds from audio start, one float per word. " +
             "Empty = full text shown at once. Populate when audio is delivered.")]
    public float[] wordTimings;

    [Tooltip("Display duration when no audio clip is assigned.")]
    [Min(0.5f)]
    public float displayDuration = 3f;

    [Tooltip("Pause after this line ends before the next begins. " +
             "Ignored when playSimultaneous = true.")]
    [Min(0f)]
    public float pauseAfter = 0.3f;
}