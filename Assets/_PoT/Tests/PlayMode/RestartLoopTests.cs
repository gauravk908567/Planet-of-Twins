using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// P8.1 (instruction.md) <c>RestartLoop_NoDuplicatesNoStaleStatics</c>: boot → the game-over Restart → boot again.
/// Afterwards there is exactly one of each Persistent manager, every static Instance points at a live object in the
/// NEW Persistent (not a survivor of the first boot), and one skill purchase raises <c>OnPointsChanged</c> exactly
/// once with no listener left over from the first boot. Restart reloading Bootstrap is where stale statics and
/// duplicate managers show up (CLAUDE.md "canary bugs").
/// </summary>
public class RestartLoopTests
{
    private List<int> _emptySlots;
    private int _pointsChangedCount;

    [SetUp]
    public void SetUp()
    {
        _emptySlots = BootHarness.EmptySlots();
        _pointsChangedCount = 0;
    }

    [TearDown]
    public void TearDown() => BootHarness.DeleteSlotsCreatedSince(_emptySlots);

    [UnityTest, Timeout(900000)]
    public IEnumerator RestartLoop_NoDuplicatesNoStaleStatics()
    {
        if (_emptySlots.Count == 0) Assert.Ignore("All save slots hold saves; free one to run the boot tests.");

        yield return BootHarness.BootNewGame(_emptySlots[0]);
        var firstBoot = PersistentSingletons.CaptureInstanceIds();

        int slot = BootHarness.FirstStillEmpty(_emptySlots);
        if (slot < 0) Assert.Ignore("The first boot filled the last empty slot; free another to run this test.");
        yield return BootHarness.RestartIntoNewGame(slot);

        var persistent = SceneFlowManager.Instance != null ? SceneFlowManager.Instance.gameObject.scene : default;
        Assert.IsTrue(persistent.IsValid(), "SceneFlowManager has no Instance after the second boot.");

        var problems = new List<string>();
        foreach (var probe in PersistentSingletons.All)
        {
            int count = probe.Count();
            if (count != probe.ExpectedCount)
                problems.Add($"{probe.Name}: {count} live (expected {probe.ExpectedCount})");

            if (probe.Instance == null || probe.SurvivesRestart) continue;
            var instance = probe.Instance();
            if (instance == null)
            {
                problems.Add($"{probe.Name}: Instance is null or destroyed");
                continue;
            }
            if (instance is Component c && c.gameObject.scene != persistent)
                problems.Add($"{probe.Name}: Instance lives in '{c.gameObject.scene.name}', not in Persistent");
            if (firstBoot.TryGetValue(probe.Name, out int firstId) && firstId == instance.GetInstanceID())
                problems.Add($"{probe.Name}: Instance is the first boot's object (it survived Restart)");
        }
        Assert.IsEmpty(problems, "After Restart:\n" + string.Join("\n", problems));

        yield return OnePurchaseRaisesPointsChangedOnce();
    }

    private IEnumerator OnePurchaseRaisesPointsChangedOnce()
    {
        var skills = SkillTreeManager.Instance;
        AbilityUpgradeData tree = null;
        foreach (var t in skills.AllTrees)
            if (t.HasNextNode) { tree = t; break; }
        Assert.IsNotNull(tree, "No skill tree has a node left to buy.");

        skills.AddPoints(Mathf.Max(1, tree.NextNodeCost));
        yield return null;

        var stale = StaleListeners(skills);
        Assert.IsEmpty(stale, "OnPointsChanged still calls destroyed objects: " + string.Join(", ", stale));

        _pointsChangedCount = 0;
        skills.OnPointsChanged += HandlePointsChanged;
        bool bought = skills.TryPurchaseNode(tree);
        skills.OnPointsChanged -= HandlePointsChanged;

        Assert.IsTrue(bought, $"Buying the next {tree.name} node failed.");
        Assert.AreEqual(1, _pointsChangedCount, "One purchase must raise OnPointsChanged exactly once.");
    }

    private void HandlePointsChanged(int total) => _pointsChangedCount++;

    // Listeners whose target is a destroyed Unity object: a subscriber from the first boot that never unsubscribed.
    private static List<string> StaleListeners(SkillTreeManager skills)
    {
        var stale = new List<string>();
        var field = typeof(SkillTreeManager).GetField(nameof(SkillTreeManager.OnPointsChanged),
                                                      BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "SkillTreeManager.OnPointsChanged has no backing field; update this test.");
        if (field.GetValue(skills) is Delegate handlers)
            foreach (var d in handlers.GetInvocationList())
                if (d.Target is Object target && target == null)
                    stale.Add($"{d.Method.DeclaringType?.Name}.{d.Method.Name}");
        return stale;
    }
}
