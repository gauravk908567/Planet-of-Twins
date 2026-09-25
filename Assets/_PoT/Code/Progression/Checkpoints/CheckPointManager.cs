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
    /// <summary>Diagnostic view of the last in-memory checkpoint (GameDebuggerV2). Read-only by convention — never mutate.</summary>
    public CheckpointData LastSaved => _saved;

    // ── Checkpoint input claim (§11.2) ────────────────────────
    // A DualCheckpoint claims X (the Accord button) while both twins stand on its nodes, so the hold means
    // "save" and not "charge Accord". AccordStateSystem reads InputClaimActive and blocks itself. Ref-counted
    // so overlapping claims can't clear each other early (only one checkpoint can hold both twins at a time,
    // but the count is the robust, R5-clean mediator for the area→Persistent hop).
    private int _inputClaims;
    public bool InputClaimActive => _inputClaims > 0;
    public void RequestInputClaim() => _inputClaims++;
    public void ReleaseInputClaim() { if (_inputClaims > 0) _inputClaims--; }

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
            checkpointLocation = location,

            // ── Save-State Contract (§11.1) — snapshot meters + world ambience + one-shot flags ──
            soulCount = SoulConvergenceSystem.Instance != null ? SoulConvergenceSystem.Instance.SoulCount : 0,
            accordBarPoints = AccordStateSystem.Instance != null ? AccordStateSystem.Instance.BarPoints : 0f,
            worldCorruption = WorldAmbienceDriver.Instance != null ? WorldAmbienceDriver.Instance.Progress : 0f,
            storyGradeId = StoryGradeDirector.Instance != null ? StoryGradeDirector.Instance.CurrentGradeId : null,
            storyProgress = StoryGradeDirector.Instance != null ? StoryGradeDirector.Instance.StoryProgress : 0f,
            skyStateId = SkyStateDriver.Instance != null ? SkyStateDriver.Instance.CurrentStateId : null,
            worldFlags = WorldFlagRegistry.Instance != null
                         ? WorldFlagRegistry.Instance.Snapshot() : System.Array.Empty<string>(),
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