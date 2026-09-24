using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Couch M2 — the one-shot WORLD-FLAG store (Persistent, R3). Records irreversible area state that a
/// save must survive: a QTE gate opened, a one-time door, a consumed switch. A flag is a stable string
/// key; once <see cref="Set"/> it stays set for the session and is written into the save slot
/// (<see cref="CheckpointData.worldFlags"/> → <see cref="GameSaveData"/>). On Continue/respawn the saved
/// keys are restored and every live flag-object is told to re-apply — WITHOUT replaying the timeline/beat
/// that originally set it (game.md §11.1 restore order).
///
/// <para>Two application paths, both covered so stream-order never matters:
///   • A flag-object streamed in AFTER a restore self-applies in <c>OnEnable</c> (queries <see cref="IsSet"/>).
///   • A flag-object already live when <see cref="Restore"/> runs is re-applied by Restore's notify pass.</para>
/// R5: area flag-objects self-register in <c>OnEnable</c> / unregister in <c>OnDisable</c>; the registry
/// null-purges before iterating (Unity fake-null via the <c>UnityEngine.Object</c> cast).
/// </summary>
[DisallowMultipleComponent]
public class WorldFlagRegistry : MonoBehaviour
{
    public static WorldFlagRegistry Instance { get; private set; }

    /// <summary>A live area object whose persistent state is driven by a world flag.</summary>
    public interface IWorldFlagObject
    {
        /// <summary>Re-apply this object's state from the current flag set (instant — no animation / no beat replay).</summary>
        void ApplyWorldFlags();
    }

    private readonly HashSet<string> _flags = new HashSet<string>();
    private readonly List<IWorldFlagObject> _objects = new List<IWorldFlagObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // R3 duplicate guard
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Flag set / query ──────────────────────────────────────
    /// <summary>Mark a flag set (idempotent). Called when a one-shot world event happens (gate opens, etc.).</summary>
    public void Set(string key)
    {
        if (!string.IsNullOrEmpty(key)) _flags.Add(key);
    }

    public bool IsSet(string key) => !string.IsNullOrEmpty(key) && _flags.Contains(key);

    /// <summary>Auto-key for placed-in-scene objects that exist in bulk (collectibles): <c>kind:scene@x,y,z</c> from
    /// the object's AUTHORED position (decimetre precision). Stable across sessions with zero authoring and immune to
    /// copy-paste duplicate keys; moving the object in the editor re-keys it (old saves just see it as uncollected).
    /// Pass the position captured in <c>Awake</c>, before any runtime bob/motion.</summary>
    public static string PositionKey(string kind, GameObject go, Vector3 authoredPos) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1}@{2:F1},{3:F1},{4:F1}",
                      kind, go.scene.name, authoredPos.x, authoredPos.y, authoredPos.z);

    /// <summary>Capture the set flags for a checkpoint (allocates — call only at save time).</summary>
    public string[] Snapshot()
    {
        var arr = new string[_flags.Count];
        _flags.CopyTo(arr);
        return arr;
    }

    // ── Restore (Continue / respawn) ──────────────────────────
    /// <summary>Replace the flag set with the saved keys, then re-apply every live flag-object.</summary>
    public void Restore(IEnumerable<string> keys)
    {
        _flags.Clear();
        if (keys != null)
            foreach (var k in keys)
                if (!string.IsNullOrEmpty(k)) _flags.Add(k);
        ReapplyAll();
    }

    /// <summary>New Game — a fresh slot carries no world flags.</summary>
    public void Clear() => _flags.Clear();

    // ── Registry (R5) ─────────────────────────────────────────
    public void Register(IWorldFlagObject obj)
    {
        if (obj != null && !_objects.Contains(obj)) _objects.Add(obj);
    }

    public void Unregister(IWorldFlagObject obj) => _objects.Remove(obj);

    private void ReapplyAll()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            // Interface refs don't honour Unity's overloaded fake-null — cast to Object to catch destroyed ones.
            if (_objects[i] as Object == null) { _objects.RemoveAt(i); continue; }
            _objects[i].ApplyWorldFlags();
        }
    }
}
