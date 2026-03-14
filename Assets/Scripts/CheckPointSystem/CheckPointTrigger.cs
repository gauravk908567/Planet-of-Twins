using UnityEngine;

/// <summary>
/// Place a Trigger collider in the level. When any Player walks through,
/// saves the current twin positions as a checkpoint.
///
/// Silent save (HUD flash via CheckpointFlashUI) - no player interaction required.
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private CheckpointManager checkpointManager;

    [Tooltip("If true, only saves once. If false, re-saves every time player re-enters.")]
    [SerializeField] private bool saveOnce = true;

    private bool _hasSaved = false;

    // Both twins needed to record their positions.
    // Stored by CheckpointManager - we just need to find them at save time.
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    private void OnTriggerEnter(Collider other)
    {
        if (saveOnce && _hasSaved) return;
        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;

        if (checkpointManager == null || leftTwin == null || rightTwin == null)
        {
            Debug.LogWarning("[CheckpointTrigger] Missing references - cannot save.", this);
            return;
        }

        checkpointManager.SaveCheckpoint(
            leftTwin.transform.position,
            rightTwin.transform.position);

        _hasSaved = true;
    }
}