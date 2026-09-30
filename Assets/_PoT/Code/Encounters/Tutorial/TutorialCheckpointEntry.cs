using UnityEngine;
/// <summary>
/// Named checkpoint entry — shown in dropdown drawer on step SOs.
/// Name is editor-only for the dropdown label. Step SOs store the hidden <see cref="id"/>, never the name or the
/// list position, so renaming or reordering entries can't break or re-point a step.
/// </summary>
[System.Serializable]
public class TutorialCheckpointEntry
{
    [Tooltip("Name shown in the checkpoint dropdown on step SOs. Free to rename: steps store the hidden id.")]
    public string name = "Checkpoint";
    public TutorialCheckpoint checkpoint;

    // Stable ID, generated once in the editor (TutorialDirector.OnValidate → EnsureCheckpointIds); never typed or shown.
    [HideInInspector] public string id;
}
