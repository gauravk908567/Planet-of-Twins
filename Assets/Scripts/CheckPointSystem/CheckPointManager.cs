using UnityEngine;

/// <summary>
/// Saves checkpoint state. On respawn, triggers a soft reset via
/// SoftResetController — enemies despawn to pool, twins reposition,
/// HP and skills restore. No scene reload.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Header("Twins")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Header("Skill tree")]
    [SerializeField] private SkillTreeManager skillTreeManager;

    [Header("HUD flash")]
    [SerializeField] private CheckpointFlashUI flashUI;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ── State ─────────────────────────────────────────────────
    public bool HasCheckpoint { get; private set; } = false;
    private CheckpointData _saved;

    // ── Public API ────────────────────────────────────────────
    public void SaveCheckpoint(Vector3 leftPos, Vector3 rightPos, WorldLocationSO location = null)
    {
        // Read sword state from each twin's PlayerAttackController
        var leftAttack = leftTwin?.GetComponent<PlayerAttackController>();
        var rightAttack = rightTwin?.GetComponent<PlayerAttackController>();

        _saved = new CheckpointData
        {
            leftTwinPosition = leftPos,
            rightTwinPosition = rightPos,
            skillPoints = skillTreeManager?.CurrentPoints ?? 0,
            skillTreeSnapshot = skillTreeManager?.TakeSkillSnapshot()
                                ?? SkillTreeRuntimeState.Snapshot.Empty,
            leftHasSword = leftAttack?.HasWeapon ?? false,
            rightHasSword = rightAttack?.HasWeapon ?? false,
            checkpointLocation = location
        };

        HasCheckpoint = true;

        // Couch M2: checkpoints are the save points — persist to the active slot (no-op if no slot chosen,
        // e.g. dev-direct boot). Disk write is fail-soft inside SaveSystem.
        SaveService.Instance?.AutoSave(_saved);

        flashUI?.Flash("Checkpoint saved");
        Debug.Log($"[CheckpointManager] Saved at L={leftPos} R={rightPos} " +
                  $"pts={_saved.skillPoints} " +
                  $"swords=({_saved.leftHasSword},{_saved.rightHasSword})");
    }

    /// <summary>
    /// Soft reset — no scene reload. Despawns enemies, repositions twins,
    /// restores HP and skill state in place via SoftResetController.
    /// </summary>
    public bool TryRespawnAtCheckpoint()
    {
        if (!HasCheckpoint)
        {
            Debug.Log("[CheckpointManager] No checkpoint saved.");
            return false;
        }

        if (SoftResetController.Instance == null)
        {
            Debug.LogError("[CheckpointManager] SoftResetController not found. " +
                           "Make sure it is in Persistent.unity.");
            return false;
        }

        SoftResetController.Instance.BeginSoftReset(_saved);
        return true;
    }

}