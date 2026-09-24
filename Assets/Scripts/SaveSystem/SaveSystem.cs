using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Couch M2 — the disk layer for save slots. Three slots, one JSON file each under
/// <see cref="Application.persistentDataPath"/>. Pure I/O + JSON; no game state, no scene residency — the
/// orchestration (which slot is active, when to auto-save, how to load-boot) lives in SaveService.
///
/// <para>All operations are fail-soft: a corrupt/unreadable file logs and returns null rather than throwing,
/// so a bad slot never bricks the front-end.</para>
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 3;

    private static string PathFor(int slot) =>
        Path.Combine(Application.persistentDataPath, $"pot_slot_{slot}.json");

    public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    /// <summary>A slot FILE exists (may still be stale/corrupt). Use for the New Game overwrite-confirm only.</summary>
    public static bool HasSave(int slot) => IsValidSlot(slot) && File.Exists(PathFor(slot));

    /// <summary>A slot holds a save that <see cref="Read"/> would actually load (current version, non-empty,
    /// parseable). The front-end gates Continue on THIS, not <see cref="HasSave"/> — a stale v1 / corrupt file must
    /// never light up a Continue that then fails. Quiet (no log) — safe to call from UI refresh.</summary>
    public static bool HasLoadableSave(int slot) => ReadInternal(slot, log: false) != null;

    public static void Write(int slot, GameSaveData data)
    {
        if (!IsValidSlot(slot) || data == null)
        {
            Debug.LogError($"[SaveSystem] Write refused — bad slot ({slot}) or null data.");
            return;
        }
        // Atomic write (§11.1): write a temp file then swap it into place, so a crash/kill mid-write can never
        // leave a half-written (corrupt) slot — the old save survives intact and the temp is discarded.
        string dest = PathFor(slot);
        string tmp = dest + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(dest)) File.Replace(tmp, dest, null);   // atomic same-volume swap
            else File.Move(tmp, dest);
            Debug.Log($"[SaveSystem] Wrote slot {slot} → {dest}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Write slot {slot} failed: {e.Message}");
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>Reads a slot, or null if empty/unreadable. Never throws.</summary>
    public static GameSaveData Read(int slot) => ReadInternal(slot, log: true);

    private static GameSaveData ReadInternal(int slot, bool log)
    {
        if (!HasSave(slot)) return null;
        try
        {
            var data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(PathFor(slot)));
            if (data == null || data.IsEmpty)
            {
                if (log) Debug.LogWarning($"[SaveSystem] Slot {slot} parsed empty/invalid — treating as no save.");
                return null;
            }
            // Version migration (§11.1): a save older than the current contract is invalidated rather than
            // half-loaded. v1 was test-only (saving never shipped enabled), so there is nothing to migrate —
            // a stale v1 file on disk must not restore a partial world. Extend here if a real v2→v3 lands.
            if (data.version < GameSaveData.CurrentVersion)
            {
                if (log) Debug.LogWarning($"[SaveSystem] Slot {slot} is v{data.version} < v{GameSaveData.CurrentVersion} " +
                                          "(pre-contract) — treating as no save.");
                return null;
            }
            return data;
        }
        catch (Exception e)
        {
            if (log) Debug.LogError($"[SaveSystem] Read slot {slot} failed: {e.Message}");
            return null;
        }
    }

    public static void Delete(int slot)
    {
        if (!HasSave(slot)) return;
        try { File.Delete(PathFor(slot)); Debug.Log($"[SaveSystem] Deleted slot {slot}."); }
        catch (Exception e) { Debug.LogError($"[SaveSystem] Delete slot {slot} failed: {e.Message}"); }
    }

    /// <summary>Slot metadata for the slot-select UI without materialising the whole save. Null when empty.</summary>
    public static GameSaveData Peek(int slot) => ReadInternal(slot, log: false);   // quiet — slot-card labels
}
