using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Couch M2 / P8.4: the save data layer headless: GameSaveData's JSON surface and v2 contract, FromCheckpoint's
/// null-safety, the world-flag store and its position keys, SaveSystem's file I/O, and the SessionSetup handoff.
/// (Was the CouchSaveSelfTest menu item.) File I/O only ever uses a slot that is EMPTY right now, and deletes it
/// after, so a real save is never touched.
/// </summary>
public class SaveDataTests
{
    private static GameSaveData Sample() => new GameSaveData
    {
        areaId = "L2_Streets",
        activeAreaIds = new[] { "L2_Streets", "L1_Park" },
        leftTwinPosition = new Vector3(1.5f, 0f, -3.25f),
        rightTwinPosition = new Vector3(-2f, 0.5f, 4f),
        skillPoints = 7,
        leftHasSword = true,
        rightHasSword = false,
        skillLevels = new[]
        {
            new GameSaveData.SkillLevelEntry { treeId = "StunData", level = 2 },
            new GameSaveData.SkillLevelEntry { treeId = "GateData", level = 1 },
        },
        // v2 Save-State Contract fields (game.md §11.1)
        soulCount = 13,
        accordBarPoints = 42.5f,
        worldCorruption = 0.35f,
        storyGradeId = "shock",
        storyProgress = 0.6f,
        skyStateId = "dusk",
        worldFlags = new[] { "gate:park_weaver", "orb:L1_Park@1.0,2.0,3.0" },
    };

    [Test]
    public void Json_RoundTripsEveryField()
    {
        var src = Sample();
        var rt = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(src, true));

        Assert.IsNotNull(rt);
        Assert.AreEqual("L2_Streets", rt.areaId);
        CollectionAssert.AreEqual(src.activeAreaIds, rt.activeAreaIds);
        Assert.AreEqual(src.leftTwinPosition, rt.leftTwinPosition);
        Assert.AreEqual(src.rightTwinPosition, rt.rightTwinPosition);
        Assert.AreEqual(7, rt.skillPoints);
        Assert.IsTrue(rt.leftHasSword && !rt.rightHasSword, "sword flags");
        Assert.AreEqual(2, rt.skillLevels.Length);
        Assert.AreEqual("StunData", rt.skillLevels[0].treeId);
        Assert.AreEqual(2, rt.skillLevels[0].level);
        Assert.IsFalse(rt.IsEmpty, "an area is set");
        Assert.IsTrue(new GameSaveData().IsEmpty, "a blank save is empty");

        // v2 contract
        Assert.AreEqual(GameSaveData.CurrentVersion, rt.version);
        Assert.AreEqual(13, rt.soulCount);
        Assert.AreEqual(42.5f, rt.accordBarPoints, 1e-5f);
        Assert.AreEqual(0.35f, rt.worldCorruption, 1e-5f);
        Assert.AreEqual(0.6f, rt.storyProgress, 1e-5f);
        Assert.AreEqual("shock", rt.storyGradeId);
        Assert.AreEqual("dusk", rt.skyStateId);
        CollectionAssert.AreEqual(src.worldFlags, rt.worldFlags);
    }

    [Test]
    public void FromCheckpoint_MapsMeters_AndNullIdsBecomeEmpty()
    {
        var cp = new CheckpointData { soulCount = 4, accordBarPoints = 9f, worldCorruption = 0.2f,
                                      storyGradeId = null, skyStateId = null, worldFlags = null };
        var fc = GameSaveData.FromCheckpoint(cp, new[] { "L1_Park" });

        Assert.AreEqual(4, fc.soulCount);
        Assert.AreEqual(9f, fc.accordBarPoints, 1e-5f);
        Assert.AreEqual("", fc.storyGradeId);
        Assert.AreEqual("", fc.skyStateId);
        Assert.IsNotNull(fc.worldFlags);
        Assert.IsEmpty(fc.worldFlags);
        CollectionAssert.AreEqual(new[] { "L1_Park" }, fc.activeAreaIds);
    }

    [Test]
    public void PositionKey_IsDeterministicDecimetreAndCultureInvariant()
    {
        var prevCulture = Thread.CurrentThread.CurrentCulture;
        var go = EditorUtility.CreateGameObjectWithHideFlags("~SaveDataTests", HideFlags.HideAndDontSave);
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");   // a ',' decimal would break keys
            string k1 = WorldFlagRegistry.PositionKey("orb", go, new Vector3(1.04f, -2.46f, 3f));
            string k2 = WorldFlagRegistry.PositionKey("orb", go, new Vector3(1.04f, -2.46f, 3f));
            Assert.AreEqual(k1, k2, "deterministic");
            StringAssert.EndsWith("@1.0,-2.5,3.0", k1, "invariant decimetre format");
            StringAssert.StartsWith("orb:" + go.scene.name + "@", k1, "kind:scene prefix");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prevCulture;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldFlagRegistry_SetIsIdempotent_RestoreReplaces_ClearEmpties()
    {
        var go = EditorUtility.CreateGameObjectWithHideFlags("~SaveDataTests", HideFlags.HideAndDontSave);
        try
        {
            var reg = go.AddComponent<WorldFlagRegistry>();   // edit mode: no Awake, pure data API
            reg.Set("a"); reg.Set("b"); reg.Set("a"); reg.Set("");
            Assert.AreEqual(2, reg.Snapshot().Length, "Set is idempotent and ignores empty keys");

            reg.Restore(new[] { "c" });
            Assert.IsFalse(reg.IsSet("a"), "Restore REPLACES (respawn rollback)");
            Assert.IsTrue(reg.IsSet("c"));

            reg.Clear();
            Assert.IsEmpty(reg.Snapshot(), "Clear (New Game)");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SaveSystem_WritesReadsOverwritesAndRejectsStaleVersions()
    {
        int slot = -1;
        for (int s = 0; s < SaveSystem.SlotCount; s++)
            if (!SaveSystem.HasSave(s)) { slot = s; break; }
        if (slot < 0) Assert.Ignore("All save slots are in use; skipped so no real save is overwritten.");

        var src = Sample();
        try
        {
            SaveSystem.Write(slot, src);
            Assert.IsTrue(SaveSystem.HasSave(slot), "HasSave after Write");
            Assert.IsTrue(SaveSystem.HasLoadableSave(slot), "HasLoadableSave after Write");
            var read = SaveSystem.Read(slot);
            Assert.AreEqual("L2_Streets", read.areaId);
            Assert.AreEqual(7, read.skillPoints);
            Assert.AreEqual(13, read.soulCount, "v2 fields come back from disk");
            Assert.AreEqual(2, read.worldFlags.Length);

            // Atomic overwrite (File.Replace): the second write wins and no .tmp is left behind.
            src.skillPoints = 11;
            SaveSystem.Write(slot, src);
            Assert.AreEqual(11, SaveSystem.Read(slot).skillPoints, "overwrite read back");
            string tmp = Path.Combine(Application.persistentDataPath, $"pot_slot_{slot}.json.tmp");
            Assert.IsFalse(File.Exists(tmp), "no .tmp left after write");

            // A stale pre-contract save exists but must not load (Continue stays greyed, the slot card reads Empty).
            src.version = 1;
            SaveSystem.Write(slot, src);
            Assert.IsTrue(SaveSystem.HasSave(slot), "stale v1 file exists");
            Assert.IsFalse(SaveSystem.HasLoadableSave(slot), "stale v1 is not loadable");
            Assert.IsNull(SaveSystem.Peek(slot), "stale v1 Peek is null");
        }
        finally
        {
            SaveSystem.Delete(slot);
        }
        Assert.IsFalse(SaveSystem.HasSave(slot), "HasSave false after Delete");
    }

    [Test]
    public void SessionSetup_CarriesModeAndSlot_AndClearResets()
    {
        try
        {
            SessionSetup.SetMode(SessionSetup.BootMode.Continue, 2);
            Assert.AreEqual(SessionSetup.BootMode.Continue, SessionSetup.Mode);
            Assert.AreEqual(2, SessionSetup.SaveSlot);
        }
        finally
        {
            SessionSetup.Clear();
        }
        Assert.AreEqual(SessionSetup.BootMode.NewGame, SessionSetup.Mode);
        Assert.AreEqual(-1, SessionSetup.SaveSlot);
    }
}
