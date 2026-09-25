using UnityEngine;
/// <summary>
/// Named checkpoint entry — shown in dropdown drawer on step SOs.
/// Name is editor-only for the dropdown label.
/// </summary>
[System.Serializable]
public class TutorialCheckpointEntry
{
    [Tooltip("Name shown in the checkpoint dropdown on step SOs.")]
    public string name = "Checkpoint";
    public TutorialCheckpoint checkpoint;
}
