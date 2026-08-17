#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Couch M3.2 self-test for <see cref="JointHoldSync"/> — the pure synchronized-hold tracker behind
/// every joint ability. EditMode tests aren't auto-discovered in this project (no editor asmdef),
/// so this is a [MenuItem] that drives the tracker through its leniency table and logs pass/fail.
///
/// Run: <b>Planet of Twins Tools ▸ Couch ▸ Test Joint-Ability Sync</b>.
/// </summary>
public static class JointAbilityGateSelfTest
{
    private const float W = 0.5f;   // leniency window (seconds)
    private const float DT = 0.1f;  // per-tick unscaled delta

    [MenuItem("Planet of Twins Tools/Couch/Test Joint-Ability Sync")]
    public static void Run()
    {
        int pass = 0, fail = 0;

        void Expect(bool cond, string label)
        {
            if (cond) pass++;
            else { fail++; Debug.LogError($"[Joint self-test] FAIL: {label}"); }
        }

        // 1. Both press the same frame → engage immediately.
        {
            var s = new JointHoldSync();
            Expect(s.Tick(true, true, W, DT), "both same frame → engaged");
        }

        // 2. Staggered WITHIN the window → engages when the second joins.
        {
            var s = new JointHoldSync();
            Expect(!s.Tick(true, false, W, DT), "solo frame 1 → not engaged");   // 0.1s solo
            Expect(!s.Tick(true, false, W, DT), "solo frame 2 → not engaged");   // 0.2s solo
            Expect(s.Tick(true, true, W, DT), "second within window → engaged");
        }

        // 3. Staggered BEYOND the window → expires; no engage until full release + resync.
        {
            var s = new JointHoldSync();
            for (int i = 0; i < 6; i++) s.Tick(true, false, W, DT);              // ~0.6s solo > 0.5 → expired
            Expect(!s.Tick(true, true, W, DT), "late partner rejected (expired)");
            Expect(!s.Tick(true, true, W, DT), "still rejected while both held");
            Expect(!s.Tick(false, false, W, DT), "both release → re-arm");
            Expect(s.Tick(true, true, W, DT), "resync after release → engaged");
        }

        // 4. Single-device fallback (both reads identical) → engages on one press.
        {
            var s = new JointHoldSync();
            Expect(s.Tick(true, true, W, DT), "single-device (p1==p2) → engaged");
        }

        // 5. Brief blip during engagement → re-syncs within the window.
        {
            var s = new JointHoldSync();
            Expect(s.Tick(true, true, W, DT), "engaged");
            Expect(!s.Tick(true, false, W, DT), "one released → disengaged");    // 0.1s solo
            Expect(s.Tick(true, true, W, DT), "re-hold within window → re-engaged");
        }

        // 6. Long release during engagement → expires (no lone re-engage until release).
        {
            var s = new JointHoldSync();
            s.Tick(true, true, W, DT);                                           // engaged
            for (int i = 0; i < 6; i++) s.Tick(true, false, W, DT);             // ~0.6s solo → expired
            Expect(!s.Tick(true, true, W, DT), "expired after long release → no re-engage");
            Expect(!s.Tick(false, false, W, DT), "both release → re-arm");
            Expect(s.Tick(true, true, W, DT), "resync → engaged");
        }

        if (fail == 0)
            Debug.Log($"[Joint self-test] ✅ Joint-ability sync: all {pass} cases passed.");
        else
            Debug.LogError($"[Joint self-test] ❌ Joint-ability sync: {pass} passed, {fail} FAILED.");
    }
}
#endif
