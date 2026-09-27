using NUnit.Framework;

/// <summary>
/// Couch M3.2: <see cref="JointHoldSync"/>, the synchronized-hold tracker behind every joint ability (Accord, Soul
/// Convergence, the checkpoint save). Leniency 0.5 s, ticked at 0.1 s unscaled. (Was the JointAbilityGateSelfTest
/// menu item.)
/// </summary>
public class JointHoldSyncTests
{
    private const float W = 0.5f;   // leniency window (seconds)
    private const float DT = 0.1f;  // per-tick unscaled delta

    [Test]
    public void BothPressSameFrame_EngagesImmediately()
    {
        var s = new JointHoldSync();
        Assert.IsTrue(s.Tick(true, true, W, DT));
    }

    [Test]
    public void SecondPlayerJoinsWithinWindow_Engages()
    {
        var s = new JointHoldSync();
        Assert.IsFalse(s.Tick(true, false, W, DT), "solo 0.1 s");
        Assert.IsFalse(s.Tick(true, false, W, DT), "solo 0.2 s");
        Assert.IsTrue(s.Tick(true, true, W, DT), "partner joins inside the window");
    }

    [Test]
    public void SecondPlayerJoinsAfterWindow_RejectedUntilBothReleaseAndResync()
    {
        var s = new JointHoldSync();
        for (int i = 0; i < 6; i++) s.Tick(true, false, W, DT);   // ~0.6 s solo > 0.5 → expired
        Assert.IsFalse(s.Tick(true, true, W, DT), "late partner rejected");
        Assert.IsFalse(s.Tick(true, true, W, DT), "still rejected while both hold");
        Assert.IsFalse(s.Tick(false, false, W, DT), "both release → re-arm");
        Assert.IsTrue(s.Tick(true, true, W, DT), "resync → engaged");
    }

    [Test]
    public void SingleDeviceFallback_EngagesOnOnePress()
    {
        var s = new JointHoldSync();
        Assert.IsTrue(s.Tick(true, true, W, DT), "both reads identical (one device) → engaged");
    }

    [Test]
    public void BriefBlipWhileEngaged_ReSyncsInsideWindow()
    {
        var s = new JointHoldSync();
        Assert.IsTrue(s.Tick(true, true, W, DT), "engaged");
        Assert.IsFalse(s.Tick(true, false, W, DT), "one released → disengaged");
        Assert.IsTrue(s.Tick(true, true, W, DT), "re-hold inside the window → engaged again");
    }

    [Test]
    public void LongReleaseWhileEngaged_ExpiresUntilBothRelease()
    {
        var s = new JointHoldSync();
        s.Tick(true, true, W, DT);                                 // engaged
        for (int i = 0; i < 6; i++) s.Tick(true, false, W, DT);   // ~0.6 s solo → expired
        Assert.IsFalse(s.Tick(true, true, W, DT), "no lone re-engage after expiry");
        Assert.IsFalse(s.Tick(false, false, W, DT), "both release → re-arm");
        Assert.IsTrue(s.Tick(true, true, W, DT), "resync → engaged");
    }
}
