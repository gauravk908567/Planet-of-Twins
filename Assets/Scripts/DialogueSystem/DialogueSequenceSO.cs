using UnityEngine;
using UnityEngine.Localization;

public enum DialogueDisplayMode { Nameplate, SubtitleBar, Both }
public enum DialogueTriggerType
{
    OnEnterZone, OnEvent, OnIdle, OnTaskProgress, Manual
}

/// <summary>
/// One SO per authored conversation or hint set.
/// All text and audio uses Unity Localisation package.
/// Add new languages in the Localisation Tables window — no SO changes needed.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Dialogue/Sequence
/// </summary>
[CreateAssetMenu(fileName = "DialogueSequence",
                 menuName = "PlanetOfTwins/Dialogue/Sequence")]
public class DialogueSequenceSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Editor-only name. Not shown to player.")]
    public string sequenceName = "New Dialogue";

    [Header("Display")]
    public DialogueDisplayMode displayMode = DialogueDisplayMode.SubtitleBar;
    [Tooltip("Lines at or below this character count use Nameplate in Both mode.")]
    public int nameplateCharLimit = 60;

    [Header("Lines")]
    public DialogueLine[] lines;

    [Header("Looping hints")]
    public bool isLooping = false;
    [Min(5f)]
    public float hintInterval = 15f;
    public DialogueLine[] hintLines;

    [Header("Behaviour")]
    public bool isInterruptible = true;
    [Min(0f)]
    public float cooldown = 30f;
    public bool skipIfBusy = true;
    [Range(0, 10)]
    public int priority = 5;
}