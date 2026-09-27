using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Area streaming (<see cref="OccupancyModel"/>, run by SceneFlowManager): the loaded set is every occupied location
/// plus its neighbours, and a location unloads only when no actor (either twin or the rescue soul) is in it and no
/// occupied location lists it as a neighbour. Synthetic locations, chain A – B – C plus an unlinked D.
/// </summary>
public class OccupancyModelTests
{
    private readonly List<WorldLocationSO> _created = new();
    private WorldLocationSO _a, _b, _c, _d;

    [SetUp]
    public void SetUp()
    {
        _a = Location("A"); _b = Location("B"); _c = Location("C"); _d = Location("D");
        _a.adjacentLocations = new[] { _b };
        _b.adjacentLocations = new[] { _a, _c };
        _c.adjacentLocations = new[] { _b };
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var so in _created) Object.DestroyImmediate(so);
        _created.Clear();
    }

    [Test]
    public void BothTwinsInOneArea_LoadsItAndItsNeighbours()
    {
        var desired = OccupancyModel.BuildDesiredSet(Actors(_a, _a, null));   // the soul isn't out
        CollectionAssert.AreEquivalent(new[] { _a, _b }, desired);
    }

    [Test]
    public void TwinsSplitAcrossNeighbours_LoadsTheUnion()
    {
        var desired = OccupancyModel.BuildDesiredSet(Actors(_a, _b));
        CollectionAssert.AreEquivalent(new[] { _a, _b, _c }, desired);
    }

    [Test]
    public void AnAreaUnloads_OnlyWhenTheLastActorHasLeft()
    {
        // Both twins walked on to C, but the rescue soul is still in A: A must stay.
        var actors = Actors(_c, _c, _a);
        var desired = OccupancyModel.BuildDesiredSet(actors);
        Assert.IsFalse(OccupancyModel.MayUnload(_a, desired, actors), "the soul still occupies A");

        // The soul follows: A is neither occupied nor a neighbour of C, so it may go; B (C's neighbour) may not.
        actors = Actors(_c, _c, _c);
        desired = OccupancyModel.BuildDesiredSet(actors);
        Assert.IsTrue(OccupancyModel.MayUnload(_a, desired, actors), "A is empty and not next to C");
        Assert.IsFalse(OccupancyModel.MayUnload(_b, desired, actors), "B is C's neighbour");
        Assert.IsFalse(OccupancyModel.MayUnload(_c, desired, actors), "C is occupied");
    }

    [Test]
    public void OccupiedGround_NeverUnloads_EvenIfMissingFromTheDesiredSet()
    {
        // The safety rule on its own: a stale or partial desired set must still never drop occupied ground.
        var actors = Actors(_d);
        Assert.IsFalse(OccupancyModel.MayUnload(_d, new HashSet<WorldLocationSO>(), actors));
        Assert.IsTrue(OccupancyModel.IsOccupied(_d, actors));
        Assert.IsFalse(OccupancyModel.IsOccupied(_a, actors));
    }

    [Test]
    public void InvalidOrMissingNeighbours_AreNotLoaded()
    {
        var noScene = Location("NoScene", sceneName: "");
        _a.adjacentLocations = new[] { _b, noScene, null };
        var desired = OccupancyModel.BuildDesiredSet(Actors(_a));
        CollectionAssert.AreEquivalent(new[] { _a, _b }, desired);
    }

    [Test]
    public void NoActorsPlaced_NothingIsDesired()
    {
        var actors = Actors(null, null);
        var desired = OccupancyModel.BuildDesiredSet(actors);
        Assert.IsEmpty(desired);
        Assert.IsTrue(OccupancyModel.MayUnload(_a, desired, actors));
    }

    private static List<WorldLocationSO> Actors(params WorldLocationSO[] locations) => new(locations);

    private WorldLocationSO Location(string name, string sceneName = null)
    {
        var so = ScriptableObject.CreateInstance<WorldLocationSO>();
        so.name = name;
        var serialized = new SerializedObject(so);
        serialized.FindProperty("scene._name").stringValue = sceneName ?? name;   // IsValid = a scene name is set
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _created.Add(so);
        return so;
    }
}
