using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Skill levels survive a checkpoint save for EVERY tree (instruction.md P8.1 `Checkpoint_RoundTrip_AllNineTrees`; pins
/// the old 7-of-9 bug, where a hand-listed tree array missed Empower and Accord State). Uses the real tree assets
/// Persistent wires, a SkillTreeManager holding them, and the game's own code at each step: AllTrees → runtime snapshot
/// → CheckpointData → GameSaveData (tree ids = asset names) → JSON → SaveService's id → tree mapping over AllTrees →
/// restored snapshot.
/// </summary>
public class SkillTreeSaveTests
{
    private GameObject _go;
    private SkillTreeManager _manager;
    private List<string> _fieldNames;
    private List<AbilityUpgradeData> _wired;

    [SetUp]
    public void SetUp()
    {
        _wired = WiredTrees(out _fieldNames);

        // A manager holding the same trees Persistent's does (edit mode: no Awake, so no singleton is touched).
        _go = new GameObject("~SkillTreeSaveTests") { hideFlags = HideFlags.HideAndDontSave };
        _manager = _go.AddComponent<SkillTreeManager>();
        var serialized = new SerializedObject(_manager);
        for (int i = 0; i < _fieldNames.Count && i < _wired.Count; i++)
            serialized.FindProperty(_fieldNames[i]).objectReferenceValue = _wired[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void Persistent_WiresEveryTreeField_WithUniquelyNamedAssets()
    {
        Assert.AreEqual(9, _fieldNames.Count, "SkillTreeManager holds 9 tree fields (update this if a tree is added)");
        Assert.AreEqual(_fieldNames.Count, _wired.Count, "every tree field is wired in Persistent");

        var names = new HashSet<string>();
        foreach (var t in _wired)
            Assert.IsTrue(names.Add(t.name), $"tree asset name '{t.name}' is used twice: a save keys trees by name");
    }

    [Test]
    public void AllTrees_ListsEveryTree_IncludingEmpowerAndAccordState()
    {
        var all = _manager.AllTrees;
        Assert.AreEqual(_wired.Count, all.Count, "AllTrees misses a tree field (the 7-of-9 bug)");
        CollectionAssert.AreEquivalent(_wired, all);
        CollectionAssert.Contains(all, _manager.EmpowerData, "Empower");
        CollectionAssert.Contains(all, _manager.AccordData, "Accord State");
    }

    [Test]
    public void CheckpointSaveRoundTrip_RestoresEveryTreesLevel()
    {
        var trees = _manager.AllTrees;
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
    public void RestoringASnapshot_ReplacesLevelsSetAfterIt()
    {
        // Death → respawn rollback: levels bought after the checkpoint are undone, including back to 0.
        var trees = _manager.AllTrees;
        var state = new SkillTreeRuntimeState();
        state.SetLevel(trees[0], 1);
        var atCheckpoint = state.TakeSnapshot();

        state.SetLevel(trees[0], 3);
        state.SetLevel(trees[trees.Count - 1], 2);
        state.RestoreSnapshot(atCheckpoint);

        Assert.AreEqual(1, state.GetLevel(trees[0]));
        Assert.AreEqual(0, state.GetLevel(trees[trees.Count - 1]));
    }

    [Test]
    public void UnknownTreeIdInASave_IsSkippedWithAWarning()
    {
        var trees = _manager.AllTrees;
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
        string text = File.ReadAllText(scenePath).Replace("\r\n", "\n");
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
