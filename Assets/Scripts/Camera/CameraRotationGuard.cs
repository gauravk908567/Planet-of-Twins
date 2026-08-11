using UnityEngine;

/// <summary>
/// Snapshots the AUTHORED local rotation of the gameplay cameras at scene load (Awake — before the tutorial
/// timeline can animate them) and re-applies it on demand. Fixes the recurring 180° camera flip (BUG-037): the
/// tutorial timeline's animation tracks leave a transpose cam at a flipped pose with no proper revert. Rather
/// than hand-edit the timeline (R11 — banned), we restore the captured rotation in the white-fade window at
/// cutscene end, so the correction happens behind the opaque screen and the player only ever sees the correct cam.
///
/// No hardcoded values — it restores whatever the cams were authored as, so re-authoring the cams just works.
///
/// SETUP: put on a Persistent GO; drag the gameplay/tutorial transpose cams (their Transforms) into `cameras`.
/// `TutorialTimelineStepSO` calls <see cref="RestoreAll"/> after the timeline finishes.
/// </summary>
public class CameraRotationGuard : MonoBehaviour
{
    [Tooltip("Cameras whose authored local rotation is snapshotted at load and restored on cutscene end " +
             "(GroupTransposeClose/Top, the tutorial pair, etc.). Drag the camera Transforms here.")]
    [SerializeField] private Transform[] cameras;

    private Quaternion[] _authored;

    private void Awake()
    {
        // Capture BEFORE anything (timeline, gameplay) can rotate them.
        _authored = new Quaternion[cameras != null ? cameras.Length : 0];
        for (int i = 0; i < _authored.Length; i++)
            if (cameras[i] != null) _authored[i] = cameras[i].localRotation;
    }

    /// <summary>Re-apply every captured authored rotation. Call in the white window at cutscene end.</summary>
    public void RestoreAll()
    {
        if (_authored == null) return;
        for (int i = 0; i < cameras.Length && i < _authored.Length; i++)
            if (cameras[i] != null) cameras[i].localRotation = _authored[i];
        Debug.Log("[CameraRotationGuard] Restored authored camera rotations (flip correction behind fade).");
    }
}
