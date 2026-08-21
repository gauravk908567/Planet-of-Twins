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

        // ── SaveSystem file I/O — only on a slot that is currently EMPTY, so a real save is never clobbered ──
        int testSlot = -1;
        for (int s = 0; s < SaveSystem.SlotCount; s++)
            if (!SaveSystem.HasSave(s)) { testSlot = s; break; }

        if (testSlot >= 0)
        {
            SaveSystem.Write(testSlot, src);
            Check(SaveSystem.HasSave(testSlot), "HasSave after Write");
            var read = SaveSystem.Read(testSlot);
            Check(read != null && read.areaId == "L2_Streets" && read.skillPoints == 7, "SaveSystem read back");
            SaveSystem.Delete(testSlot);
            Check(!SaveSystem.HasSave(testSlot), "HasSave false after Delete");
        }
        else
        {
            Debug.LogWarning("[SaveSelfTest] All 3 slots occupied — skipped file I/O test (won't clobber real saves).");
        }

        Debug.Log($"[SaveSelfTest] {pass} passed, {fail} failed.");
    }
}
