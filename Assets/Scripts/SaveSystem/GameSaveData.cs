using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Couch M2 — disk-serializable snapshot of one save slot. Mirrors the in-memory <see cref="CheckpointData"/>
/// but replaces every ScriptableObject reference with a stable string id (the asset name), because
/// <see cref="JsonUtility"/> cannot serialize SO references. Ids are resolved back to SOs on load via
/// <see cref="SceneFlowManager"/> (locations) and <see cref="SkillTreeManager.AllTrees"/> (upgrade trees).
///
/// <para>Save = the checkpoint state at the moment a checkpoint fires (auto-save). Continue streams
/// <see cref="areaId"/> and re-applies the progress. See <see cref="SaveSystem"/> for the file layer.</para>
/// </summary>
[Serializable]
public class GameSaveData
{
    // v1 → v2 (2026-09-23): added the Save-State Contract fields (meters + world ambience + world flags,
    // game.md §11.1). v1 slots were test-only (saving was never enabled for a real playthrough) → invalidated
    // on read in SaveSystem, so no field-level migration is needed.
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public string savedAtUtc = "";        // ISO-8601 (round-trip "o") — the slot card's timestamp
    public string areaId = "";            // WorldLocationSO.name of the checkpoint area (Continue boots here)
    public string areaName = "";          // player-facing name for the slot card (WorldLocationSO.DisplayName at save
                                          // time); older saves lack it → the card derives one from areaId
    public string[] activeAreaIds = Array.Empty<string>();  // locations loaded at save time (occupancy seed)

    public Vector3 leftTwinPosition;
    public Vector3 rightTwinPosition;

    public int skillPoints;
    public bool leftHasSword;
    public bool rightHasSword;

    public SkillLevelEntry[] skillLevels = Array.Empty<SkillLevelEntry>();

    // ── Save-State Contract (§11.1) — mirrors the new CheckpointData fields ──
    public int soulCount;
    public float accordBarPoints;
    public float worldCorruption;
    public string storyGradeId = "";
    public float storyProgress;
    public string skyStateId = "";
    public string[] worldFlags = Array.Empty<string>();

    /// <summary>An empty slot has no area — Continue/load is offered only for non-empty slots.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(areaId);

    [Serializable]
    public struct SkillLevelEntry
    {
        public string treeId;   // AbilityUpgradeData.name (asset name — stable id)
        public int level;
    }

    // ── Build a save DTO from an in-memory checkpoint (SO refs → string ids) ──
    // activeAreaIds is passed in explicitly: CheckpointData doesn't carry the live loaded-set, so the
    // save hook reads it from SceneFlowManager at save time (SaveService).
    public static GameSaveData FromCheckpoint(CheckpointData cp, IEnumerable<string> activeAreaIds = null)
    {
        if (cp == null) return null;
        return new GameSaveData
        {
            version = CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            areaId = cp.checkpointLocation != null ? cp.checkpointLocation.name : "",
            areaName = cp.checkpointLocation != null ? cp.checkpointLocation.DisplayName : "",
            activeAreaIds = activeAreaIds != null ? new List<string>(activeAreaIds).ToArray() : IdsOf(cp.activeLocations),
            leftTwinPosition = cp.leftTwinPosition,
            rightTwinPosition = cp.rightTwinPosition,
            skillPoints = cp.skillPoints,
            leftHasSword = cp.leftHasSword,
            rightHasSword = cp.rightHasSword,
            skillLevels = SkillEntriesOf(cp.skillTreeSnapshot),

            // §11.1 contract fields (SO-free primitives/ids — mirror straight across)
            soulCount = cp.soulCount,
            accordBarPoints = cp.accordBarPoints,
            worldCorruption = cp.worldCorruption,
            storyGradeId = cp.storyGradeId ?? "",
            storyProgress = cp.storyProgress,
            skyStateId = cp.skyStateId ?? "",
            worldFlags = cp.worldFlags ?? Array.Empty<string>(),
        };
    }

    private static string[] IdsOf(WorldLocationSO[] locs)
    {
        if (locs == null) return Array.Empty<string>();
        var list = new List<string>(locs.Length);
        foreach (var l in locs) if (l != null) list.Add(l.name);
        return list.ToArray();
    }

    private static SkillLevelEntry[] SkillEntriesOf(SkillTreeRuntimeState.Snapshot snap)
    {
        if (snap?.Levels == null) return Array.Empty<SkillLevelEntry>();
        var list = new List<SkillLevelEntry>();
        foreach (var kv in snap.Levels)
            if (kv.Key != null && kv.Value > 0)
                list.Add(new SkillLevelEntry { treeId = kv.Key.name, level = kv.Value });
        return list.ToArray();
    }
}
