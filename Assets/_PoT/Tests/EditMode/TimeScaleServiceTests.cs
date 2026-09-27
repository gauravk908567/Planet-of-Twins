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
