using UnityEngine;

/// <summary>
/// Place a Trigger collider in the level. When any Player walks through,
/// saves the current twin positions as a checkpoint.
///
/// Silent save (HUD flash via CheckpointFlashUI) — no player interaction required.
/// Wire `location` to this area's WorldLocationSO asset so the restore sequence
/// can stream in the correct chunk before teleporting.
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("This area's WorldLocationSO — used by SoftResetController to ensure correct area is loaded.")]
    [SerializeField] private WorldLocationSO location;

    [Tooltip("If true, only saves once. If false, re-saves every time player re-enters.")]
    [SerializeField] private bool saveOnce = true;

    /// <summary>Read-only — for the Scene Health "Checkpoints" recipe / debugger (never null-checked at save).</summary>
    public WorldLocationSO Location => location;

    private bool _hasSaved = false;
    private bool _insideDual;   // authoring guard (BUG-117) — see Awake
    private CheckpointManager _checkpointManager;
    private Player _leftTwin;
    private Player _rightTwin;

    private void Awake()
    {
        // A plain trigger nested inside a DualCheckpoint (e.g. an old checkpoint visual reused as a node) would save on
        // ONE twin's touch and bypass the both-twins hold (BUG-117). Disabling isn't enough — trigger messages still
        // reach disabled behaviours — so it goes inert and says so. Scene Health flags the same thing as FAIL.
        _insideDual = GetComponentInParent<DualCheckpoint>(true) != null;
        if (_insideDual)
            Debug.LogWarning($"[CheckpointTrigger] '{name}' sits inside a DualCheckpoint — ignored (the dual checkpoint " +
                             "owns saving there). Remove this component.", this);
    }

    private void Start()
    {
        if (_insideDual) return;
        _checkpointManager = CheckpointManager.Instance;
        if (_checkpointManager == null)
            Debug.LogError("[CheckpointTrigger] CheckpointManager.Instance is null.", this);

        _leftTwin  = PlayerRoster.Instance?.TwinA;
        _rightTwin = PlayerRoster.Instance?.TwinB;
        if (_leftTwin == null || _rightTwin == null)
            Debug.LogError("[CheckpointTrigger] PlayerRoster twins are null.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_insideDual) return;
        if (saveOnce && _hasSaved) return;
        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;

        if (_checkpointManager == null || _leftTwin == null || _rightTwin == null)
        {
            Debug.LogWarning("[CheckpointTrigger] Missing references — cannot save.", this);
            return;
        }

        _checkpointManager.SaveCheckpoint(
            _leftTwin.transform.position,
            _rightTwin.transform.position,
            location);

        _hasSaved = true;
    }
}