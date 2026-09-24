using UnityEditor;
using UnityEngine;

/// <summary>
/// Couch M2 — headless round-trip check for the save data layer (GameSaveData + SaveSystem). Verified via
/// [MenuItem] because EditMode tests are not auto-discovered in this project (no editor asmdef).
/// Menu: Planet of Twins Tools ▸ Couch ▸ Test Save Round-Trip.
/// </summary>
public static class CouchSaveSelfTest
{
    [MenuItem("Planet of Twins Tools/Couch/Test Save Round-Trip")]
    public static void Run()
    {
        int pass = 0, fail = 0;
        void Check(bool ok, string label)
        {
            if (ok) { pass++; }
            else { fail++; Debug.LogError($"[SaveSelfTest] FAIL: {label}"); }
        }

        var src = new GameSaveData
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
            // v2 Save-State Contract fields (§11.1)
            soulCount = 13,
            accordBarPoints = 42.5f,
            worldCorruption = 0.35f,
            storyGradeId = "shock",
            storyProgress = 0.6f,
            skyStateId = "dusk",
            worldFlags = new[] { "gate:park_weaver", "orb:L1_Park@1.0,2.0,3.0" },
        };

        // ── JSON round-trip (the serialization surface: Vector3 + arrays + struct array) ──
        string json = JsonUtility.ToJson(src, true);
        var rt = JsonUtility.FromJson<GameSaveData>(json);
        Check(rt != null, "FromJson non-null");
        Check(rt.areaId == "L2_Streets", "areaId");
        Check(rt.activeAreaIds != null && rt.activeAreaIds.Length == 2 && rt.activeAreaIds[1] == "L1_Park", "activeAreaIds");
        Check(rt.leftTwinPosition == src.leftTwinPosition, "leftTwinPosition");
        Check(rt.rightTwinPosition == src.rightTwinPosition, "rightTwinPosition");
        Check(rt.skillPoints == 7, "skillPoints");
        Check(rt.leftHasSword && !rt.rightHasSword, "sword flags");
        Check(rt.skillLevels != null && rt.skillLevels.Length == 2, "skillLevels count");
        Check(rt.skillLevels[0].treeId == "StunData" && rt.skillLevels[0].level == 2, "skillLevels[0]");
        Check(!rt.IsEmpty, "IsEmpty false when area set");
        Check(new GameSaveData().IsEmpty, "IsEmpty true when blank");

        // ── v2 contract fields survive JSON ──
        Check(rt.version == GameSaveData.CurrentVersion, "version defaults to CurrentVersion");
        Check(rt.soulCount == 13 && Mathf.Approximately(rt.accordBarPoints, 42.5f), "meters (souls/bar)");
        Check(Mathf.Approximately(rt.worldCorruption, 0.35f) && Mathf.Approximately(rt.storyProgress, 0.6f), "ambience floats");
        Check(rt.storyGradeId == "shock" && rt.skyStateId == "dusk", "grade/sky ids");
        Check(rt.worldFlags != null && rt.worldFlags.Length == 2 && rt.worldFlags[1] == "orb:L1_Park@1.0,2.0,3.0", "worldFlags");

        // ── FromCheckpoint maps the contract + null-safes ids/flags ──
        var cp = new CheckpointData { soulCount = 4, accordBarPoints = 9f, worldCorruption = 0.2f,
                                      storyGradeId = null, skyStateId = null, worldFlags = null };
        var fc = GameSaveData.FromCheckpoint(cp, new[] { "L1_Park" });
        Check(fc.soulCount == 4 && Mathf.Approximately(fc.accordBarPoints, 9f), "FromCheckpoint meters");
        Check(fc.storyGradeId == "" && fc.skyStateId == "" && fc.worldFlags != null && fc.worldFlags.Length == 0,
              "FromCheckpoint null ids/flags → empty");
        Check(fc.activeAreaIds.Length == 1 && fc.activeAreaIds[0] == "L1_Park", "FromCheckpoint activeAreaIds");

        // ── Orb auto-key: deterministic, decimetre, culture-invariant (a ',' decimal would break keys) ──
        var prevCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        var go = EditorUtility.CreateGameObjectWithHideFlags("~SaveSelfTest", HideFlags.HideAndDontSave);
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            string k1 = WorldFlagRegistry.PositionKey("orb", go, new Vector3(1.04f, -2.46f, 3f));
            string k2 = WorldFlagRegistry.PositionKey("orb", go, new Vector3(1.04f, -2.46f, 3f));
            Check(k1 == k2, "PositionKey deterministic");
            Check(k1.EndsWith("@1.0,-2.5,3.0"), $"PositionKey invariant decimetre format (got '{k1}')");
            Check(k1.StartsWith("orb:" + go.scene.name + "@"), "PositionKey kind:scene prefix");

            // ── WorldFlagRegistry store semantics (Awake doesn't run in edit mode; pure data API) ──
            var reg = go.AddComponent<WorldFlagRegistry>();
            reg.Set("a"); reg.Set("b"); reg.Set("a"); reg.Set("");
            Check(reg.Snapshot().Length == 2, "registry Set idempotent, ignores empty");
            reg.Restore(new[] { "c" });
            Check(!reg.IsSet("a") && reg.IsSet("c"), "registry Restore REPLACES (respawn rollback)");
            reg.Clear();
            Check(reg.Snapshot().Length == 0, "registry Clear (New Game)");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prevCulture;
            Object.DestroyImmediate(go);
        }

        // ── SaveSystem file I/O — only on a slot that is currently EMPTY, so a real save is never clobbered ──
        int testSlot = -1;
        for (int s = 0; s < SaveSystem.SlotCount; s++)
            if (!SaveSystem.HasSave(s)) { testSlot = s; break; }

        if (testSlot >= 0)
        {
            try
            {
                SaveSystem.Write(testSlot, src);
                Check(SaveSystem.HasSave(testSlot), "HasSave after Write");
                Check(SaveSystem.HasLoadableSave(testSlot), "HasLoadableSave after Write");
                var read = SaveSystem.Read(testSlot);
                Check(read != null && read.areaId == "L2_Streets" && read.skillPoints == 7, "SaveSystem read back");
                Check(read != null && read.soulCount == 13 && read.worldFlags.Length == 2, "v2 fields read back from disk");

                // Atomic overwrite path (File.Replace) — second write wins, no .tmp left behind.
                src.skillPoints = 11;
                SaveSystem.Write(testSlot, src);
                var read2 = SaveSystem.Read(testSlot);
                Check(read2 != null && read2.skillPoints == 11, "overwrite (atomic Replace) read back");
                string tmp = System.IO.Path.Combine(Application.persistentDataPath, $"pot_slot_{testSlot}.json.tmp");
                Check(!System.IO.File.Exists(tmp), "no .tmp left after write");

                // Stale pre-contract save: file exists but must NOT be loadable (Continue stays greyed).
                src.version = 1;
                SaveSystem.Write(testSlot, src);
                Check(SaveSystem.HasSave(testSlot), "stale v1 file exists");
                Check(!SaveSystem.HasLoadableSave(testSlot), "stale v1 NOT loadable");
                Check(SaveSystem.Peek(testSlot) == null, "stale v1 Peek null (slot card reads Empty)");
                src.version = GameSaveData.CurrentVersion;
            }
            finally
            {
                SaveSystem.Delete(testSlot);
            }
            Check(!SaveSystem.HasSave(testSlot), "HasSave false after Delete");

            // ── Front-end → Persistent handoff (SessionSetup is the only thing that crosses) ──
            SessionSetup.SetMode(SessionSetup.BootMode.Continue, testSlot);
            Check(SessionSetup.Mode == SessionSetup.BootMode.Continue && SessionSetup.SaveSlot == testSlot, "SessionSetup records mode+slot");
            SessionSetup.Clear();
            Check(SessionSetup.Mode == SessionSetup.BootMode.NewGame && SessionSetup.SaveSlot == -1, "SessionSetup.Clear resets");
        }
        else
        {
            Debug.LogWarning("[SaveSelfTest] All 3 slots occupied — skipped file I/O test (won't clobber real saves).");
        }

        Debug.Log($"[SaveSelfTest] {pass} passed, {fail} failed.");
    }
}
