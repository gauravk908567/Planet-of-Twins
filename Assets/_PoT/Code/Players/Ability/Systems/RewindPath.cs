using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The smooth return path of a rewind (Setsuna's; the reference feel is Tracer's Recall). Built once from a recorded
/// trail of positions, then sampled by the share of the distance travelled, so the caller moves at an even speed along
/// a smooth curve and picks the easing itself.
///
/// How <see cref="FromRecording"/> builds it:
///   1. Order: the current position first, then the recording newest → oldest. 0 = here, 1 = the oldest sample.
///   2. Split wherever two samples are farther apart than <c>blinkDistance</c> (a teleport). The path crosses that gap
///      in zero distance (a blink) instead of flying through whatever lay between.
///   3. Drop samples within <c>minStep</c> of the last one kept, so standing still never replays as a stop.
///   4. Simplify (Ramer–Douglas–Peucker, <c>tolerance</c>): the small wiggles go, the corners stay.
///   5. Cut long straight spans into pieces of at most <c>8 × tolerance</c>. A curve through a sharp corner swings
///      wide on the outside by about a tenth of the spans next to it; short spans keep that swing near the tolerance.
///   6. Centripetal Catmull–Rom through every kept point (no loops, no cusps), tessellated and measured, so
///      <see cref="Evaluate"/> works by distance.
/// Pure maths, no scene access; covered by RewindPathTests.
/// </summary>
public sealed class RewindPath
{
    private const int StepsPerSpan = 8;
    private const float SpanPerTolerance = 8f;

    // One continuous piece per stretch between blinks: tessellated points + the distance along the piece at each.
    private readonly List<Vector3[]> _pieces = new();
    private readonly List<float[]> _distances = new();

    /// <summary>Where the path starts (the position the rewind begins from).</summary>
    public Vector3 Start { get; }
    /// <summary>Where the path ends (the oldest recorded sample: the cast position).</summary>
    public Vector3 End { get; }
    /// <summary>Total travelled length in metres; blinks add nothing.</summary>
    public float Length { get; }
    /// <summary>How many teleport gaps the path blinks across.</summary>
    public int BlinkCount => _pieces.Count - 1;

    /// <param name="recorded">Positions in the order they were recorded (oldest first). May be empty.</param>
    /// <param name="current">Where the actor is now; the path starts here.</param>
    /// <param name="minStep">Metres: samples closer than this to the last kept one count as standing still.</param>
    /// <param name="tolerance">Metres the smoothed path may stray from the recording. 0 = no simplifying.</param>
    /// <param name="blinkDistance">Metres: a longer jump between two samples was a teleport. 0 = never blink.</param>
    public static RewindPath FromRecording(IReadOnlyList<Vector3> recorded, Vector3 current,
                                           float minStep, float tolerance, float blinkDistance)
    {
        var ordered = new List<Vector3>(recorded.Count + 1) { current };
        for (int i = recorded.Count - 1; i >= 0; i--) ordered.Add(recorded[i]);
        return new RewindPath(ordered, minStep, tolerance, blinkDistance);
    }

    private RewindPath(List<Vector3> ordered, float minStep, float tolerance, float blinkDistance)
    {
        Start = ordered[0];
        End = ordered[ordered.Count - 1];

        float blinkSqr = blinkDistance > 0f ? blinkDistance * blinkDistance : float.PositiveInfinity;
        var run = new List<Vector3> { ordered[0] };
        for (int i = 1; i < ordered.Count; i++)
        {
            if ((ordered[i] - ordered[i - 1]).sqrMagnitude > blinkSqr)
            {
                AddPiece(run, minStep, tolerance);
                run = new List<Vector3>();
            }
            run.Add(ordered[i]);
        }
        AddPiece(run, minStep, tolerance);

        float length = 0f;
        foreach (var d in _distances) length += d[d.Length - 1];
        Length = length;
    }

    /// <summary>
    /// The point <paramref name="share"/> of the way along the path by distance: 0 = <see cref="Start"/>,
    /// 1 = <see cref="End"/>. Equal steps in share are equal steps in metres, so an even share rate is an even speed.
    /// </summary>
    public Vector3 Evaluate(float share)
    {
        if (share >= 1f) return End;
        if (share <= 0f || Length <= 0f) return Start;

        float d = share * Length;
        for (int p = 0; p < _pieces.Count; p++)
        {
            var distances = _distances[p];
            float pieceLength = distances[distances.Length - 1];
            if (d <= pieceLength || p == _pieces.Count - 1)
                return Sample(_pieces[p], distances, Mathf.Min(d, pieceLength));
            d -= pieceLength;   // past this piece: the blink to the next one costs no distance
        }
        return End;
    }

    // ── Build steps ─────────────────────────────────────────────────────────────

    private void AddPiece(List<Vector3> run, float minStep, float tolerance)
    {
        var kept = CapSpans(Simplify(DropStill(run, minStep), tolerance), tolerance * SpanPerTolerance);
        var points = Tessellate(kept);
        var distances = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
            distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        _pieces.Add(points);
        _distances.Add(distances);
    }

    // Keeps the first and last sample exactly; drops any sample within minStep of the last one kept.
    private static List<Vector3> DropStill(List<Vector3> run, float minStep)
    {
        var kept = new List<Vector3> { run[0] };
        float minSqr = minStep * minStep;
        for (int i = 1; i < run.Count; i++)
            if ((run[i] - kept[kept.Count - 1]).sqrMagnitude >= minSqr) kept.Add(run[i]);

        Vector3 last = run[run.Count - 1];
        if (kept[kept.Count - 1] != last)
        {
            if (kept.Count > 1) kept[kept.Count - 1] = last;
            else kept.Add(last);
        }
        return kept;
    }

    // Ramer–Douglas–Peucker, measured to the SEGMENT (not the infinite line), so a path that doubles back on itself
    // keeps its far end instead of collapsing onto the line.
    private static List<Vector3> Simplify(List<Vector3> points, float tolerance)
    {
        if (points.Count < 3 || tolerance <= 0f) return points;

        var keep = new bool[points.Count];
        keep[0] = keep[points.Count - 1] = true;
        var ranges = new Stack<(int from, int to)>();
        ranges.Push((0, points.Count - 1));
        while (ranges.Count > 0)
        {
            var (from, to) = ranges.Pop();
            float worst = 0f;
            int worstIndex = -1;
            for (int i = from + 1; i < to; i++)
            {
                float d = DistanceToSegment(points[i], points[from], points[to]);
                if (d > worst) { worst = d; worstIndex = i; }
            }
            if (worstIndex < 0 || worst <= tolerance) continue;
            keep[worstIndex] = true;
            ranges.Push((from, worstIndex));
            ranges.Push((worstIndex, to));
        }

        var result = new List<Vector3>();
        for (int i = 0; i < points.Count; i++) if (keep[i]) result.Add(points[i]);
        return result;
    }

    // Splits every span longer than maxSpan into equal straight parts (collinear points keep a straight run straight).
    private static List<Vector3> CapSpans(List<Vector3> points, float maxSpan)
    {
        if (points.Count < 2 || maxSpan <= 0f) return points;

        var result = new List<Vector3> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            int parts = Mathf.CeilToInt(Vector3.Distance(a, b) / maxSpan);
            for (int k = 1; k < parts; k++) result.Add(Vector3.Lerp(a, b, k / (float)parts));
            result.Add(b);
        }
        return result;
    }

    private static Vector3[] Tessellate(List<Vector3> k)
    {
        int n = k.Count;
        if (n <= 2) return k.ToArray();   // a single point or one straight hop: nothing to smooth

        var result = new Vector3[(n - 1) * StepsPerSpan + 1];
        int w = 0;
        for (int s = 0; s < n - 1; s++)
        {
            Vector3 p0 = s > 0 ? k[s - 1] : 2f * k[0] - k[1];               // mirrored phantom ends
            Vector3 p3 = s + 2 < n ? k[s + 2] : 2f * k[n - 1] - k[n - 2];
            result[w++] = k[s];                                            // exact at every kept point
            for (int step = 1; step < StepsPerSpan; step++)
                result[w++] = CentripetalCatmullRom(p0, k[s], k[s + 1], p3, step / (float)StepsPerSpan);
        }
        result[w] = k[n - 1];
        return result;
    }

    // ── Maths ───────────────────────────────────────────────────────────────────

    // Barry–Goldman form, alpha 0.5: the point u (0..1) of the way from p1 to p2.
    private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
    {
        const float t0 = 0f;
        float t1 = t0 + Knot(p0, p1);
        float t2 = t1 + Knot(p1, p2);
        float t3 = t2 + Knot(p2, p3);
        float t = Mathf.Lerp(t1, t2, u);

        Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
        Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
        Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
        Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
        Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
        return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
    }

    // Centripetal knot spacing: the square root of the distance (floored so near-duplicate points never divide by 0).
    private static float Knot(Vector3 a, Vector3 b) => Mathf.Max(Mathf.Sqrt(Vector3.Distance(a, b)), 1e-4f);

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        float t = lengthSqr > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    private static Vector3 Sample(Vector3[] points, float[] distances, float d)
    {
        if (points.Length == 1) return points[0];
        int lo = 0, hi = points.Length - 1;   // invariant: distances[lo] <= d
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (distances[mid] <= d) lo = mid; else hi = mid;
        }
        float span = distances[hi] - distances[lo];
        return span > 0f ? Vector3.Lerp(points[lo], points[hi], (d - distances[lo]) / span) : points[hi];
    }
}
