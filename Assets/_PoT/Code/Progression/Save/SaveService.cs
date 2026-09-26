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
    [Tooltip("Kill switch for DISK WRITES. OFF = AutoSave never writes a slot (checkpoints still work in-memory " +
             "for respawn). ON since the Save-State Contract landed (game.md §11.1, 2026-09-23). NOTE: the front-end " +
             "(FrontEnd scene) can't read this — it runs before Persistent loads — so the slot screen + Continue are " +
             "gated on the DISK instead (SaveSystem.HasLoadableSave): with this OFF no new saves appear, so Continue " +
             "only lights for saves written while it was ON.")]
    [SerializeField] private bool enableSaving = false;

    /// <summary>Disk-write kill switch — see <see cref="enableSaving"/>.</summary>
    public bool SavingEnabled => enableSaving;

    /// <summary>True only during a Continue load's settle window. Area-embedded story beats
    /// (<see cref="SkyboxMaterialChange"/>) that auto-fire on scene load read this and skip, so they can't
    /// stomp the world ambience the load just restored (§11.1 break #2). Forward-progress beats fire normally
    /// once the window closes. Set/cleared by <see cref="GameBootstrapper"/> around the load.</summary>
    public bool SuppressStoryBeats { get; private set; }
    public void BeginLoadSuppressBeats() => SuppressStoryBeats = true;
    public void EndLoadSuppressBeats() => SuppressStoryBeats = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ApplySessionSetup();
    }

    // The front-end (FrontEnd scene) runs BEFORE Persistent exists, so it records its New Game / Continue + slot
    // choice in SessionSetup; we apply it the moment Persistent loads (same handoff PlayerRoster uses for the twin
    // pick). Awake = self-wiring from a static — no other Persistent object is needed (R8). Before the Continue
    // boot path reads PendingLoad: GameBootstrapper awaits the Persistent load, so Awake has always run by then.
    // No slot chosen (dev-direct boot, direct area play, TestLab) → ActiveSlot stays NoSlot → nothing hits disk.
    private void ApplySessionSetup()
    {
        int slot = SessionSetup.SaveSlot;
        if (!SaveSystem.IsValidSlot(slot)) return;

        if (SessionSetup.Mode == SessionSetup.BootMode.Continue)
        {
            if (!BeginContinue(slot))
                Debug.LogError($"[SaveService] Front-end chose Continue on slot {slot} but it's unreadable now — " +
                               "the boot path will fall back to a New Game in that slot.", this);
        }
        else
        {
            BeginNewGame(slot);
        }
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
        WorldFlagRegistry.Instance?.Clear();   // a fresh start carries no opened-gate / one-shot world flags
        PoTLog.Crumb(PoTCrumb.Flow, $"New Game (slot {ActiveSlot})");
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
        PoTLog.Crumb(PoTCrumb.Flow, $"Continue (slot {slot}, area '{data.areaId}')");
        Debug.Log($"[SaveService] Continue → slot {slot}, area '{data.areaId}'.");
        return true;
    }

    /// <summary>Boot path calls this once the pending load has been consumed (area streamed + progress applied).</summary>
    public void ClearPendingLoad() => PendingLoad = null;

    // ── Auto-save (CheckpointManager calls this after building its checkpoint) ──
    public void AutoSave(CheckpointData cp)
    {
        if (!enableSaving) return;   // feature dormant until enableSaving is flipped (SCOPE 1)
        if (ActiveSlot == NoSlot || cp == null) return;

        // Invariant diagnostic (§11.1): checkpoints sit in calm zones, so no ability should be live at save
        // time. This never force-ends the running session (force-end is load-only) — it just flags a mislaid
        // checkpoint. Coalesce is EXEMPT (passive, enemy-tied).
        WarnIfAbilityActiveAtSave();

        IEnumerable<string> active = SceneFlowManager.Instance != null
            ? SceneFlowManager.Instance.LoadedLocationIds : null;
        SaveSystem.Write(ActiveSlot, GameSaveData.FromCheckpoint(cp, active));
    }

    private static void WarnIfAbilityActiveAtSave()
    {
        bool active =
            (SoulConvergenceSystem.Instance != null && SoulConvergenceSystem.Instance.IsAbilityActive) ||
            (AccordStateSystem.Instance != null && (AccordStateSystem.Instance.IsAccordActive ||
                (AccordStateSystem.Instance.VoidStrikeActiveState?.IsAbilityActive ?? false) ||
                (AccordStateSystem.Instance.RadiantSeekerActiveState?.IsAbilityActive ?? false))) ||
            (SetsunaSystem.Instance != null && SetsunaSystem.Instance.IsAbilityActive) ||
            (EmpowerSystem.Instance != null && EmpowerSystem.Instance.IsAbilityActive);
        if (active)
            Debug.LogWarning("[SaveService] Checkpoint auto-saved while an ability was ACTIVE — a checkpoint " +
                             "should sit in a calm zone (§11.1). Saving progression state anyway; the load " +
                             "path force-ends abilities regardless.");
    }

    // ── Load-apply (Continue) — skills/points/sword. Positions + area streaming are the boot path's job
    //    (it owns the spawn placement). Reuses the exact idioms SoftResetController uses so restore behaves
    //    identically to a checkpoint respawn. ──
    public void ApplyProgress(GameSaveData data)
    {
        if (data == null) return;
        RestoreSkills(data);   // skills FIRST — SC only accrues once unlocked, Accord's cap is upgrade-derived (§11.1)
        RestoreSwords(data);
        RestoreWorldAndMeters(data.soulCount, data.accordBarPoints, data.worldCorruption,
                              data.storyGradeId, data.storyProgress, data.skyStateId, data.worldFlags);
    }

    /// <summary>Restore the §11.1 contract's runtime state — ability meters + the 3 world-ambience drivers +
    /// one-shot world flags — to the checkpoint's values. Shared by the Continue load (<see cref="ApplyProgress"/>,
    /// from <see cref="GameSaveData"/>) and death→respawn (<see cref="SoftResetController"/>, from
    /// <see cref="CheckpointData"/>): both pass identical primitives, so restore is identical either way.
    /// Callers MUST have restored skills first. Instant, no beat replay. Safe when any singleton is absent.</summary>
    public static void RestoreWorldAndMeters(int soulCount, float accordBarPoints,
        float worldCorruption, string storyGradeId, float storyProgress, string skyStateId,
        string[] worldFlags)
    {
        // Meters (derived flags _charged/_barFull recomputed inside each Restore*).
        SoulConvergenceSystem.Instance?.RestoreSouls(soulCount);
        AccordStateSystem.Instance?.RestoreBar(accordBarPoints);

        // World ambience — the 3 story drivers, applied instantly.
        WorldAmbienceDriver.Instance?.SetProgress(worldCorruption);
        var grade = StoryGradeDirector.Instance;
        if (grade != null)
        {
            grade.SetStoryProgress(storyProgress);
            // Re-pick the EXACT saved grade after setting progress — event-only grades (minProgress < 0,
            // e.g. "shock") can't be reached by progress alone (§11.1).
            if (!string.IsNullOrEmpty(storyGradeId)) grade.PlayGrade(storyGradeId);
        }
        if (!string.IsNullOrEmpty(skyStateId)) SkyStateDriver.Instance?.ApplyState(skyStateId);

        // One-shot world flags (opened gates, one-time doors) — set keys + re-apply to live area objects.
        WorldFlagRegistry.Instance?.Restore(worldFlags);
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
