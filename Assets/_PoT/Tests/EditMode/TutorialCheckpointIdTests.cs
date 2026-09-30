using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Tutorial checkpoint steps store each checkpoint's hidden stable id (<see cref="TutorialCheckpointEntry.id"/>),
/// never its list position or its name, so reordering or renaming the TutorialDirector's checkpoint list can't
/// re-point or break a step (<see cref="TutorialStepContext.GetCheckpoint"/>, <see cref="TutorialStepContext.EnsureCheckpointIds"/>).
/// </summary>
public class TutorialCheckpointIdTests
{
    private readonly List<GameObject> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _created) Object.DestroyImmediate(go);
        _created.Clear();
    }

    [Test]
    public void EnsureCheckpointIds_GivesEveryEntryAUniqueId()
    {
        var ctx = Context(Entry("A"), Entry("B"), Entry("C"));
        Assert.IsTrue(ctx.EnsureCheckpointIds());
        var ids = new HashSet<string>();
        foreach (var e in ctx.checkpoints)
        {
            Assert.IsFalse(string.IsNullOrEmpty(e.id), e.name);
            Assert.IsTrue(ids.Add(e.id), "duplicate id on " + e.name);
        }
        Assert.IsFalse(ctx.EnsureCheckpointIds(), "a second pass changes nothing");
    }

    [Test]
    public void EnsureCheckpointIds_DuplicatedEntryGetsAFreshId_OriginalKeepsItsId()
    {
        var original = Entry("A", "keep-me");
        var copy = Entry("A copy", "keep-me");   // Inspector "duplicate element" copies the id
        var ctx = Context(original, copy);
        Assert.IsTrue(ctx.EnsureCheckpointIds());
        Assert.AreEqual("keep-me", original.id);
        Assert.AreNotEqual("keep-me", copy.id);
    }

    [Test]
    public void GetCheckpoint_SurvivesReorderAndRename()
    {
        var a = Entry("Gate A"); var b = Entry("Gate B"); var c = Entry("Park A");
        var ctx = Context(a, b, c);
        ctx.EnsureCheckpointIds();
        string storedByStep = b.id;

        ctx.checkpoints = new[] { c, b, a };   // reordered
        b.name = "Renamed gate";               // relabelled
        Assert.AreSame(b.checkpoint, ctx.GetCheckpoint(storedByStep));
    }

    [Test]
    public void GetCheckpoint_RemovedEntry_ReturnsNullAndLogsError()
    {
        var ctx = Context(Entry("A", "id-a"));
        LogAssert.Expect(LogType.Error, new Regex("No checkpoint entry has id 'id-gone'"));
        Assert.IsNull(ctx.GetCheckpoint("id-gone"));
    }

    [Test]
    public void GetCheckpoint_SharedId_ReturnsNullAndLogsError()
    {
        var ctx = Context(Entry("A", "same"), Entry("B", "same"));
        LogAssert.Expect(LogType.Error, new Regex("2 checkpoint entries share id 'same'"));
        Assert.IsNull(ctx.GetCheckpoint("same"));
    }

    private static TutorialStepContext Context(params TutorialCheckpointEntry[] entries) =>
        new TutorialStepContext { checkpoints = entries };

    private TutorialCheckpointEntry Entry(string name, string id = null)
    {
        var go = new GameObject(name);
        _created.Add(go);
        return new TutorialCheckpointEntry { name = name, id = id, checkpoint = go.AddComponent<TutorialCheckpoint>() };
    }
}
