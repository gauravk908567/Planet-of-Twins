using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Skill levels survive a checkpoint save for EVERY tree SkillTreeManager holds (the bug class: a hand-listed tree
/// array that missed one). Uses the real tree assets Persistent wires, and the game's own mapping at each step:
/// runtime snapshot → CheckpointData → GameSaveData (tree ids = asset names) → JSON → SaveService's id → tree
/// mapping → restored snapshot.
/// </summary>
public class SkillTreeSaveTests
{
    [Test]
    public void Persistent_WiresEveryTreeField_WithUniquelyNamedAssets()
    {
        var trees = WiredTrees(out var fieldNames);
        Assert.AreEqual(9, fieldNames.Count, "SkillTreeManager holds 9 tree fields (update this if a tree is added)");
        Assert.AreEqual(fieldNames.Count, trees.Count, "every tree field is wired in Persistent");

        var names = new HashSet<string>();
        foreach (var t in trees)
            Assert.IsTrue(names.Add(t.name), $"tree asset name '{t.name}' is used twice: a save keys trees by name");
    }

    [Test]
    public void CheckpointSaveRoundTrip_RestoresEveryTreesLevel()
    {
        var trees = WiredTrees(out _);
        var state = new SkillTreeRuntimeState();
        for (int i = 0; i < trees.Count; i++) state.SetLevel(trees[i], i + 1);   // a distinct level per tree

        var checkpoint = new CheckpointData { skillTreeSnapshot = state.TakeSnapshot() };
        string json = JsonUtility.ToJson(GameSaveData.FromCheckpoint(checkpoint, new[] { "L1_Park" }));
        var loaded = JsonUtility.FromJson<GameSaveData>(json);

        var restored = new SkillTreeRuntimeState();
        restored.RestoreSnapshot(SaveService.SkillSnapshotFromEntries(trees, loaded.skillLevels));

        for (int i = 0; i < trees.Count; i++)
            Assert.AreEqual(i + 1, restored.GetLevel(trees[i]), $"level of '{trees[i].name}'");
    }

    [Test]
    public void UnknownTreeIdInASave_IsSkippedWithAWarning()
    {
        var trees = WiredTrees(out _);
        var entries = new[]
        {
            new GameSaveData.SkillLevelEntry { treeId = trees[0].name, level = 2 },
            new GameSaveData.SkillLevelEntry { treeId = "NoSuchTree", level = 5 },
        };

        LogAssert.Expect(LogType.Warning, new Regex("Unknown skill tree id 'NoSuchTree'"));
        var snapshot = SaveService.SkillSnapshotFromEntries(trees, entries);

        Assert.AreEqual(1, snapshot.Levels.Count);
        Assert.AreEqual(2, snapshot.Levels[trees[0]]);
    }

    // The AbilityUpgradeData assets Persistent's SkillTreeManager points at, one per tree field (field order).
    // Read from the scene file so the test needs no scene load (opening Persistent runs its edit-mode tools).
    private static List<AbilityUpgradeData> WiredTrees(out List<string> fieldNames)
    {
        fieldNames = new List<string>();
        foreach (var f in typeof(SkillTreeManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            if (f.FieldType == typeof(AbilityUpgradeData)) fieldNames.Add(f.Name);

        string scenePath = FindAsset("Persistent", "t:Scene");
        string scriptGuid = AssetDatabase.AssetPathToGUID(FindAsset("SkillTreeManager", "t:MonoScript"));
        string text = File.ReadAllText(scenePath);
        int script = text.IndexOf("guid: " + scriptGuid, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(script, 0, "Persistent has a SkillTreeManager");
        int blockEnd = text.IndexOf("\n--- ", script, System.StringComparison.Ordinal);
        string block = text.Substring(script, (blockEnd < 0 ? text.Length : blockEnd) - script);

        var trees = new List<AbilityUpgradeData>();
        foreach (var field in fieldNames)
        {
            var m = Regex.Match(block, $@"\n  {field}: {{fileID: \d+, guid: ([0-9a-f]{{32}})");
            if (!m.Success) continue;
            var tree = AssetDatabase.LoadAssetAtPath<AbilityUpgradeData>(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value));
            if (tree != null) trees.Add(tree);
        }
        return trees;
    }

    private static string FindAsset(string name, string filter)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{name} {filter}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return path;
        }
        Assert.Fail($"asset '{name}' ({filter}) not found");
        return null;
    }
}
