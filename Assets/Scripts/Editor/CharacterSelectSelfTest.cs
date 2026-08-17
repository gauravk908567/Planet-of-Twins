#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Couch M2 self-test for <see cref="CharacterSelectController.TryResolve"/> — the pure distinct-twin
/// resolver. EditMode tests aren't auto-discovered in this project (no editor asmdef), so this is a
/// [MenuItem] that asserts the whole resolution table and logs pass/fail.
///
/// Run: <b>Planet of Twins Tools ▸ Couch ▸ Test Character-Select Resolution</b>.
/// </summary>
public static class CharacterSelectSelfTest
{
    [MenuItem("Planet of Twins Tools/Couch/Test Character-Select Resolution")]
    public static void Run()
    {
        int pass = 0, fail = 0;

        void Expect(bool cond, string label)
        {
            if (cond) pass++;
            else { fail++; Debug.LogError($"[CS self-test] FAIL: {label}"); }
        }

        // Both explicit + distinct → passthrough, startable.
        Expect(CharacterSelectController.TryResolve(CharacterPick.Lyra, CharacterPick.Kai, true, out var a, out var b)
               && a == CharacterPick.Lyra && b == CharacterPick.Kai, "Lyra + Kai → (Lyra, Kai)");
        Expect(CharacterSelectController.TryResolve(CharacterPick.Kai, CharacterPick.Lyra, true, out a, out b)
               && a == CharacterPick.Kai && b == CharacterPick.Lyra, "Kai + Lyra → (Kai, Lyra)");

        // One Random → resolves to the complement of the explicit pick.
        Expect(CharacterSelectController.TryResolve(CharacterPick.Lyra, CharacterPick.Random, true, out a, out b)
               && a == CharacterPick.Lyra && b == CharacterPick.Kai, "Lyra + Random → (Lyra, Kai)");
        Expect(CharacterSelectController.TryResolve(CharacterPick.Random, CharacterPick.Kai, true, out a, out b)
               && a == CharacterPick.Lyra && b == CharacterPick.Kai, "Random + Kai → (Lyra, Kai)");
        Expect(CharacterSelectController.TryResolve(CharacterPick.Kai, CharacterPick.Random, true, out a, out b)
               && a == CharacterPick.Kai && b == CharacterPick.Lyra, "Kai + Random → (Kai, Lyra)");
        Expect(CharacterSelectController.TryResolve(CharacterPick.Random, CharacterPick.Lyra, true, out a, out b)
               && a == CharacterPick.Kai && b == CharacterPick.Lyra, "Random + Lyra → (Kai, Lyra)");

        // Both Random → coin decides P1, always distinct.
        Expect(CharacterSelectController.TryResolve(CharacterPick.Random, CharacterPick.Random, true, out a, out b)
               && a == CharacterPick.Lyra && b == CharacterPick.Kai, "Random + Random (coin=true) → (Lyra, Kai)");
        Expect(CharacterSelectController.TryResolve(CharacterPick.Random, CharacterPick.Random, false, out a, out b)
               && a == CharacterPick.Kai && b == CharacterPick.Lyra, "Random + Random (coin=false) → (Kai, Lyra)");

        // Same explicit twin → NOT startable (returns false).
        Expect(!CharacterSelectController.TryResolve(CharacterPick.Kai, CharacterPick.Kai, true, out _, out _),
               "Kai + Kai → blocked (won't start)");
        Expect(!CharacterSelectController.TryResolve(CharacterPick.Lyra, CharacterPick.Lyra, true, out _, out _),
               "Lyra + Lyra → blocked (won't start)");

        if (fail == 0)
            Debug.Log($"[CS self-test] ✅ Character-select resolution: all {pass} cases passed.");
        else
            Debug.LogError($"[CS self-test] ❌ Character-select resolution: {pass} passed, {fail} FAILED.");
    }
}
#endif
