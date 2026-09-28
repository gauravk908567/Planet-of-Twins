using NUnit.Framework;
using UnityEngine;

/// <summary>
/// R10: every Time.timeScale write goes through <see cref="TimeScaleService"/>, and the lowest request wins
/// (Setsuna 0.15 under a pause 0 stays 0; releasing the pause drops back to Setsuna, not to 1).
/// </summary>
public class TimeScaleServiceTests
{
    private GameObject _go;
    private TimeScaleService _service;
    private float _savedTimeScale;
    private readonly object _setsuna = new object();
    private readonly object _pause = new object();

    [SetUp]
    public void SetUp()
    {
        _savedTimeScale = Time.timeScale;
        _go = new GameObject("~TimeScaleServiceTests") { hideFlags = HideFlags.HideAndDontSave };
        _service = _go.AddComponent<TimeScaleService>();   // edit mode: no Awake, so no singleton is touched
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        Time.timeScale = _savedTimeScale;
    }

    [Test]
    public void NoRequests_ScaleIsOne()
    {
        _service.Request(_setsuna, 0.5f);
        _service.Release(_setsuna);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void LowestRequestWins()
    {
        _service.Request(_setsuna, 0.15f);
        _service.Request(_pause, 0f);
        Assert.AreEqual(0f, Time.timeScale, "pause under Setsuna");

        _service.Release(_pause);
        Assert.AreEqual(0.15f, Time.timeScale, 1e-6f, "unpausing returns to Setsuna, not to 1");

        _service.Release(_setsuna);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void MinWins_InEveryRequestAndReleaseOrder()
    {
        // instruction.md P8.1: every order of the game's real values (pause 0, Setsuna 0.15, 0.25, 0.85), requested one
        // by one and then released one by one: the scale is always the lowest value still held, 1 when none is.
        float[] values = { 0f, 0.15f, 0.25f, 0.85f };
        foreach (var order in Permutations(new[] { 0, 1, 2, 3 }))
        {
            var owners = new object[4];
            float held = 1f;
            foreach (int i in order)
            {
                owners[i] = new object();
                _service.Request(owners[i], values[i]);
                held = Mathf.Min(held, values[i]);
                Assert.AreEqual(held, Time.timeScale, 1e-6f, $"after requesting {values[i]} (order {string.Join(",", order)})");
            }
            for (int k = 0; k < order.Length; k++)
            {
                _service.Release(owners[order[k]]);
                float expected = 1f;
                for (int r = k + 1; r < order.Length; r++) expected = Mathf.Min(expected, values[order[r]]);
                Assert.AreEqual(expected, Time.timeScale, 1e-6f, $"after releasing {values[order[k]]} (order {string.Join(",", order)})");
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<int[]> Permutations(int[] items, int start = 0)
    {
        if (start == items.Length - 1) { yield return (int[])items.Clone(); yield break; }
        for (int i = start; i < items.Length; i++)
        {
            (items[start], items[i]) = (items[i], items[start]);
            foreach (var p in Permutations(items, start + 1)) yield return p;
            (items[start], items[i]) = (items[i], items[start]);
        }
    }

    [Test]
    public void ReRequest_UpdatesTheSameOwner()
    {
        _service.Request(_setsuna, 0.15f);
        _service.Request(_setsuna, 0.6f);
        Assert.AreEqual(0.6f, Time.timeScale, 1e-6f, "one owner holds one value");
    }

    [Test]
    public void Requests_AreClampedToZeroOne()
    {
        _service.Request(_setsuna, 3f);
        Assert.AreEqual(1f, Time.timeScale, "above 1 clamps to 1");
        _service.Request(_setsuna, -2f);
        Assert.AreEqual(0f, Time.timeScale, "below 0 clamps to 0");
    }

    [Test]
    public void ReleaseAll_ClearsEveryOwner()
    {
        _service.Request(_setsuna, 0.15f);
        _service.Request(_pause, 0f);
        _service.ReleaseAll();
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void ReleasingAnUnknownOwner_IsHarmless()
    {
        _service.Request(_setsuna, 0.15f);
        _service.Release(new object());
        Assert.AreEqual(0.15f, Time.timeScale, 1e-6f);
    }
}
