using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Couch M2 — the save orchestrator (Persistent, R3). Owns the ACTIVE slot, auto-saves the current session on
/// every checkpoint, and stages a chosen save for the Continue boot path. Sits above <see cref="SaveSystem"/>
/// (disk) and converts <see cref="CheckpointData"/> ↔ <see cref="GameSaveData"/>, resolving SO ids via
/// <see cref="SceneFlowManager"/> (locations) and <see cref="SkillTreeManager"/> (upgrade trees).
///
/// <para>Wiring: an empty GO in Persistent with this component (no serialized refs — it resolves managers by
/// singleton in R4 style at call time). Duplicate-destroy Awake guard + null Instance on destroy (R3).</para>
/// </summary>
[DisallowMultipleComponent]
public class SaveService : MonoBehaviour
{
    public static SaveService Instance { get; private set; }

    public const int NoSlot = -1;

    /// <summary>The slot the current session auto-saves to. <see cref="NoSlot"/> = don't persist (dev-direct
    /// boot never chose a slot). Set by the front-end on New Game / Continue.</summary>
    public int ActiveSlot { get; private set; } = NoSlot;

    /// <summary>A save chosen via Continue, staged for the boot path to apply. Null = New Game (fresh start).</summary>
    public GameSaveData PendingLoad { get; private set; }

    /// <summary>True for the rest of the session once a Continue is chosen (cleared by the next New Game). The
    /// area's <see cref="TutorialDirector"/> reads this to SKIP the tutorial on a resumed save — a load boots
    /// straight into gameplay, past the tutorial, even when the saved area is the tutorial area (L2_Streets).
    /// Kept beyond <see cref="PendingLoad"/> because the director's Start may run after the boot clears it.</summary>
    public bool IsResumingSave { get; private set; }

    [Header("Feature gate")]
    [Tooltip("MASTER SWITCH for save/Continue. OFF (default) = the whole feature is DORMANT: AutoSave never " +
             "writes, the Continue button stays disabled (even if a stale test-save exists on disk), and the " +
             "front-end skips the save-slot screen. This is deliberate: a checkpoint sits AFTER a timeline that " +
             "mutates a large amount of world state (skybox/lighting, story grading, Weaver's Gate QTE " +
             "completion, and a long tail of per-system flags) that the current snapshot does NOT capture — a " +
             "half-restored Continue is worse than none. The real save-state CONTRACT is deferred until after " +
             "the couch conversion (which changes WHAT state exists). Flip this ON only alongside that contract.")]
    [SerializeField] private bool enableSaving = false;

    /// <summary>Master switch — see <see cref="enableSaving"/>. When false the feature is fully inert.</summary>
    public bool SavingEnabled => enableSaving;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Front-end API (MainMenu / save-slot screen) ───────────
    public bool HasSave(int slot) => SaveSystem.HasSave(slot);
    public GameSaveData Peek(int slot) => SaveSystem.Peek(slot);

    /// <summary>New Game into <paramref name="slot"/> — it becomes the active save target (overwritten at the
    /// first checkpoint). Clears any pending load so the boot path takes the fresh-start (intro) route.</summary>
    public void BeginNewGame(int slot)
    {
        ActiveSlot = SaveSystem.IsValidSlot(slot) ? slot : NoSlot;
        PendingLoad = null;
        IsResumingSave = false;
        Debug.Log($"[SaveService] New Game → active slot {ActiveSlot}.");
    }

    /// <summary>Continue from <paramref name="slot"/> — reads the save, makes it the active target, and stages
    /// it for the boot path. Returns false if the slot is empty/unreadable (front-end keeps the slot screen up).</summary>
    public bool BeginContinue(int slot)
    {
        var data = SaveSystem.Read(slot);
        if (data == null) { Debug.LogWarning($"[SaveService] Continue slot {slot} — no readable save."); return false; }
        ActiveSlot = slot;
        PendingLoad = data;
        IsResumingSave = true;
        Debug.Log($"[SaveService] Continue → slot {slot}, area '{data.areaId}'.");
        return true;
    }

    /// <summary>Boot path calls this once the pending load has been consumed (area streamed + progress applied).</summary>
    public void ClearPendingLoad() => PendingLoad = null;

    // ── Auto-save (CheckpointManager calls this after building its checkpoint) ──
    public void AutoSave(CheckpointData cp)
    {
        if (!enableSaving) return;   // feature dormant — see enableSaving (world-state contract deferred)
        if (ActiveSlot == NoSlot || cp == null) return;
        IEnumerable<string> active = SceneFlowManager.Instance != null
            ? SceneFlowManager.Instance.LoadedLocationIds : null;
        SaveSystem.Write(ActiveSlot, GameSaveData.FromCheckpoint(cp, active));
    }

    // ── Load-apply (Continue) — skills/points/sword. Positions + area streaming are the boot path's job
    //    (it owns the spawn placement). Reuses the exact idioms SoftResetController uses so restore behaves
    //    identically to a checkpoint respawn. ──
    public void ApplyProgress(GameSaveData data)
    {
        if (data == null) return;
        RestoreSkills(data);
        RestoreSwords(data);
    }

    private static void RestoreSkills(GameSaveData data)
    {
        var stm = SkillTreeManager.Instance;
        if (stm == null) return;

        // Points: drain current, add saved (mirrors SoftResetController.RestoreSkillTree — no SetPoints exists).
        if (stm.CurrentPoints > 0) stm.TrySpendPoints(stm.CurrentPoints);
        stm.AddPoints(data.skillPoints);

        // Levels: ids → trees → snapshot. RebuildUnlockFlags fires inside RestoreSkillSnapshot.
        var dict = new Dictionary<AbilityUpgradeData, int>();
        if (data.skillLevels != null)
            foreach (var e in data.skillLevels)
            {
                var tree = FindTree(stm, e.treeId);
                if (tree != null) dict[tree] = e.level;
                else Debug.LogWarning($"[SaveService] Unknown skill tree id '{e.treeId}' in save — skipped.");
            }
        stm.RestoreSkillSnapshot(new SkillTreeRuntimeState.Snapshot(dict));
    }

    private static AbilityUpgradeData FindTree(SkillTreeManager stm, string id)
    {
        foreach (var t in stm.AllTrees) if (t != null && t.name == id) return t;
        return null;
    }

    private static void RestoreSwords(GameSaveData data)
    {
        var roster = PlayerRoster.Instance;
        if (roster == null) return;
        roster.TwinA?.GetComponentInChildren<PlayerAttackController>(true)?.SetHasWeapon(data.leftHasSword);
        roster.TwinB?.GetComponentInChildren<PlayerAttackController>(true)?.SetHasWeapon(data.rightHasSword);

        // Re-arm the streamed area's sword pickups to match the save (mirrors SoftResetController.RestoreSwords,
        // BUG-090): a pickup is available exactly when its twin did NOT have the sword at save time.
        foreach (var pickup in Object.FindObjectsByType<SwordPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool collected = pickup.IsForLeftTwin ? data.leftHasSword : data.rightHasSword;
            pickup.gameObject.SetActive(!collected);
        }
    }
}
