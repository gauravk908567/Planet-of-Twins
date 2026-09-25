using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only progress for all skill trees. Owned by SkillTreeManager (Persistent).
/// Plain C# class — no ScriptableObject, no MonoBehaviour, no scene residency.
///
/// Replaces the R7-violating AbilityUpgradeData.currentNodeIndex mutable field.
/// AbilityUpgradeData.currentNodeIndex now delegates reads to SkillTreeManager.Instance.GetLevel(this).
/// </summary>
public class SkillTreeRuntimeState
{
    private readonly Dictionary<AbilityUpgradeData, int> _levels = new();

    public int GetLevel(AbilityUpgradeData data) =>
        data != null && _levels.TryGetValue(data, out int v) ? v : 0;

    public void SetLevel(AbilityUpgradeData data, int level)
    {
        if (data == null) return;
        _levels[data] = Mathf.Max(0, level);
    }

    public Snapshot TakeSnapshot() => new Snapshot(_levels);

    public void RestoreSnapshot(Snapshot snap)
    {
        _levels.Clear();
        if (snap?.Levels == null) return;
        foreach (var kv in snap.Levels)
            if (kv.Key != null) _levels[kv.Key] = kv.Value;
    }

    /// <summary>
    /// In-memory capture of all upgrade levels at a point in time.
    /// Stored on CheckpointData and passed to SoftResetController for restoration.
    /// </summary>
    public class Snapshot
    {
        public readonly IReadOnlyDictionary<AbilityUpgradeData, int> Levels;

        public Snapshot(Dictionary<AbilityUpgradeData, int> src) =>
            Levels = new Dictionary<AbilityUpgradeData, int>(src);

        public static readonly Snapshot Empty =
            new Snapshot(new Dictionary<AbilityUpgradeData, int>());
    }
}
