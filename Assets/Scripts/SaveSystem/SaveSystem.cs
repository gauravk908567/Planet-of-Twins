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

    public static bool HasSave(int slot) => IsValidSlot(slot) && File.Exists(PathFor(slot));

    public static void Write(int slot, GameSaveData data)
    {
        if (!IsValidSlot(slot) || data == null)
        {
            Debug.LogError($"[SaveSystem] Write refused — bad slot ({slot}) or null data.");
            return;
        }
        try
        {
            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
            Debug.Log($"[SaveSystem] Wrote slot {slot} → {PathFor(slot)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Write slot {slot} failed: {e.Message}");
        }
    }

    /// <summary>Reads a slot, or null if empty/unreadable. Never throws.</summary>
    public static GameSaveData Read(int slot)
    {
        if (!HasSave(slot)) return null;
        try
        {
            var data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(PathFor(slot)));
            if (data == null || data.IsEmpty)
            {
                Debug.LogWarning($"[SaveSystem] Slot {slot} parsed empty/invalid — treating as no save.");
                return null;
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Read slot {slot} failed: {e.Message}");
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
    public static GameSaveData Peek(int slot) => Read(slot);
}
