using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves checkpoint state. On respawn, reloads the scene entirely
/// (fresh enemies, traps released, AI reset) then repositions twins
/// and restores skill state via a persistent loader object.
///
/// "Fresh game from a new spawn point" — enemies, traps, AI all reset
/// as if the game just started, but players appear at the checkpoint.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    [Header("Twins")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Header("Skill tree")]
    [SerializeField] private SkillTreeManager skillTreeManager;

    [Header("HUD flash")]
    [SerializeField] private CheckpointFlashUI flashUI;

    // ── State ─────────────────────────────────────────────────
    public bool HasCheckpoint { get; private set; } = false;
    private CheckpointData _saved;

    // ── Persistent loader ─────────────────────────────────────
    // On respawn we reload the scene. A DontDestroyOnLoad object carries
    // the saved data across the reload and applies it after the new scene loads.
    private static CheckpointLoader _pendingLoader;

    // ── Public API ────────────────────────────────────────────
    public void SaveCheckpoint(Vector3 leftPos, Vector3 rightPos)
    {
        _saved = new CheckpointData
        {
            leftTwinPosition = leftPos,
            rightTwinPosition = rightPos,
            skillPoints = skillTreeManager?.CurrentPoints ?? 0,
            nodeUnlockLevels = CaptureNodeLevels()
        };

        HasCheckpoint = true;
        flashUI?.Flash("Checkpoint saved");
        Debug.Log($"[CheckpointManager] Saved at L={leftPos} R={rightPos} " +
                  $"pts={_saved.skillPoints}");
    }

    /// <summary>
    /// Reloads the scene fresh (enemies/traps reset) then repositions
    /// twins and restores skill state via a persistent CheckpointLoader.
    /// </summary>
    public bool TryRespawnAtCheckpoint()
    {
        if (!HasCheckpoint)
        {
            Debug.Log("[CheckpointManager] No checkpoint saved.");
            return false;
        }

        // Create a persistent loader that survives the scene reload
        if (_pendingLoader != null)
            Destroy(_pendingLoader.gameObject);

        var go = new GameObject("CheckpointLoader");
        DontDestroyOnLoad(go);
        _pendingLoader = go.AddComponent<CheckpointLoader>();
        _pendingLoader.Initialise(_saved);

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        return true;
    }

    // ── Helpers ────────────────────────────────────────────────
    private int[] CaptureNodeLevels()
    {
        if (skillTreeManager == null) return new int[0];

        var allData = new List<AbilityUpgradeData>
        {
            skillTreeManager.StunData,
            skillTreeManager.PossessData,
            skillTreeManager.GateData,
            skillTreeManager.HealthRegenData,
            skillTreeManager.AccordSpiritsData,
            skillTreeManager.CoalesceData,
            skillTreeManager.SoulConvData
        };

        int[] levels = new int[allData.Count];
        for (int i = 0; i < allData.Count; i++)
            levels[i] = allData[i]?.CurrentUnlockedLevel ?? 0;
        return levels;
    }
}