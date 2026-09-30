using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <see cref="RewindPath"/>, the smooth return path behind Setsuna's rewind (the Tracer's Recall feel, 2026-09-30).
/// A recording is oldest-first, as <c>SetsunaSystem.HandleActive</c> records it; the path runs from the current
/// position back to the recording's first sample (the cast position).
/// </summary>
public class RewindPathTests
{
    private const float MinStep = 0.05f;
    private const float Tolerance = 0.3f;
    private const float Blink = 8f;

    private static RewindPath Build(List<Vector3> recorded, Vector3 current) =>
        RewindPath.FromRecording(recorded, current, MinStep, Tolerance, Blink);

    // Straight walk from a to b, one sample every `step` metres, b included.
    private static void Walk(List<Vector3> into, Vector3 a, Vector3 b, float step)
    {
        int n = Mathf.CeilToInt(Vector3.Distance(a, b) / step);
        for (int i = 0; i <= n; i++) into.Add(Vector3.Lerp(a, b, i / (float)n));
    }

    private static void Stand(List<Vector3> into, Vector3 at, int samples)
    {
        for (int i = 0; i < samples; i++) into.Add(at);
    }

    private static float DistanceToPolyline(Vector3 p, List<Vector3> line)
    {
        float best = float.MaxValue;
        for (int i = 1; i < line.Count; i++)
        {
            Vector3 ab = line[i] - line[i - 1];
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - line[i - 1], ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector3.Distance(p, line[i - 1] + ab * t));
        }
        return best;
    }

    [Test]
    public void Endpoints_AreExact()
    {
        var rec = new List<Vector3> { new(0, 0, 0), new(1, 0, 0.4f), new(2, 0.5f, 1), new(3, 0, 0) };
        var current = new Vector3(4, 0, 0.2f);
        var path = Build(rec, current);

        Assert.AreEqual(current, path.Evaluate(0f), "starts where the twin is now");
        Assert.AreEqual(rec[0], path.Evaluate(1f), "ends exactly on the cast position");
    }

    [Test]
    public void StandingStill_DoesNotPauseTheReturn()
    {
        var rec = new List<Vector3>();
        Stand(rec, Vector3.zero, 40);                                  // 2 s idle at the cast spot
        Walk(rec, Vector3.zero, new Vector3(10, 0, 0), 1f);
        Stand(rec, new Vector3(10, 0, 0), 40);                         // 2 s idle at the far end
        var path = Build(rec, new Vector3(10, 0, 0));

        Assert.AreEqual(10f, path.Length, 0.01f);
        Assert.AreEqual(5f, path.Evaluate(0.5f).x, 0.01f, "halfway in share = halfway in metres, idles cost nothing");
    }

    [Test]
    public void Speed_IsEvenAlongTheCurve()
    {
        var rec = new List<Vector3>();
        Walk(rec, Vector3.zero, new Vector3(10, 0, 0), 0.2f);          // dense samples
        Walk(rec, new Vector3(10, 0, 0), new Vector3(10, 0, 10), 1.5f); // sparse samples round a corner
        var path = Build(rec, new Vector3(10, 0, 10));

        float expected = path.Length / 100f;
        for (int i = 0; i < 100; i++)
        {
            float step = Vector3.Distance(path.Evaluate(i / 100f), path.Evaluate((i + 1) / 100f));
            Assert.That(step, Is.InRange(expected * 0.8f, expected * 1.001f), $"step {i}");
        }
    }

    [Test]
    public void Wiggles_AreSmoothedAway()
    {
        var rec = new List<Vector3>();
        for (int i = 0; i <= 40; i++)                                  // a 10 m line with ±0.1 m zigzag
            rec.Add(new Vector3(i * 0.25f, 0, i == 0 || i == 40 ? 0f : (i % 2 == 0 ? 0.1f : -0.1f)));
        var path = Build(rec, rec[rec.Count - 1]);

        Assert.AreEqual(10f, path.Length, 0.05f, "the zigzag (~12.8 m) replays as the straight line");
        for (int i = 0; i <= 50; i++)
            Assert.AreEqual(0f, path.Evaluate(i / 50f).z, 0.001f, $"sample {i} is on the line");
    }

    [Test]
    public void Corners_AreKept_AndTheSwingStaysSmall()
    {
        var rec = new List<Vector3>();
        Walk(rec, Vector3.zero, new Vector3(10, 0, 0), 0.5f);
        Walk(rec, new Vector3(10, 0, 0), new Vector3(10, 0, 10), 0.5f);
        var path = Build(rec, new Vector3(10, 0, 10));

        float nearestToCorner = float.MaxValue, widestSwing = 0f;
        for (int i = 0; i <= 1000; i++)
        {
            Vector3 p = path.Evaluate(i / 1000f);
            nearestToCorner = Mathf.Min(nearestToCorner, Vector3.Distance(p, new Vector3(10, 0, 0)));
            widestSwing = Mathf.Max(widestSwing, DistanceToPolyline(p, rec));
        }
        Assert.Less(nearestToCorner, 0.05f, "the curve still goes through the corner");
        Assert.LessOrEqual(widestSwing, Tolerance, "never strays farther than the tolerance (walls near a corner)");
    }

    [Test]
    public void OutAndBack_KeepsTheFarEnd()
    {
        var rec = new List<Vector3>();
        Walk(rec, Vector3.zero, new Vector3(10, 0, 0), 0.5f);
        Walk(rec, new Vector3(10, 0, 0), new Vector3(2, 0, 0), 0.5f);
        var path = Build(rec, new Vector3(2, 0, 0));

        float farthest = 0f;
        for (int i = 0; i <= 1000; i++) farthest = Mathf.Max(farthest, path.Evaluate(i / 1000f).x);
        Assert.AreEqual(10f, farthest, 0.05f, "the return retraces the run out to x = 10 and back");
        Assert.AreEqual(18f, path.Length, 0.1f);
    }

    [Test]
    public void TeleportGap_IsBlinked_NotFlown()
    {
        var rec = new List<Vector3>();
        Walk(rec, Vector3.zero, new Vector3(5, 0, 0), 0.5f);
        Walk(rec, new Vector3(50, 0, 0), new Vector3(55, 0, 0), 0.5f); // a 45 m jump between two samples
        var path = Build(rec, new Vector3(55, 0, 0));

        Assert.AreEqual(1, path.BlinkCount);
        Assert.AreEqual(10f, path.Length, 0.05f, "only the walked metres count");
        for (int i = 0; i <= 1000; i++)
        {
            float x = path.Evaluate(i / 1000f).x;
            Assert.IsFalse(x > 5.01f && x < 49.99f, $"sample {i} (x = {x}) is inside the teleport gap");
        }
    }

    [Test]
    public void NoMovement_StaysPut()
    {
        var at = new Vector3(1, 2, 3);
        var rec = new List<Vector3>();
        Stand(rec, at, 20);
        var path = Build(rec, at);

        Assert.AreEqual(0f, path.Length);
        Assert.AreEqual(at, path.Evaluate(0.3f));
        Assert.AreEqual(at, path.Evaluate(1f));
    }
}
