using System.Collections.Generic;

/// <summary>
/// Pure, scene-free scheduling math for Cue Books. This is the "OccupancyModel move": the
/// timing logic lives apart from any Unity object so it can be unit-tested directly. Lives in
/// <c>PlanetOfTwins.FxCore</c>.
///
/// This computes the precomputable timeline assuming every element's duration is KNOWN (finite). It is the
/// tested ORACLE for the math and is used by <see cref="CueBookRunner"/> for finite-only books. When a book
/// contains a HELD element (looping VFX / particle — duration 0 here), an <c>AfterPreviousCompletion</c>
/// element behind it cannot be precomputed (its real "completion" is a runtime cut / gameplay Stop); the runner
/// resolves those at runtime. For all-finite books the runner's result equals this precompute (guarded by tests).
/// </summary>
public static class CueSchedule
{
    /// <summary>One step reduced to just the numbers the scheduler needs (no Unity refs).</summary>
    public struct StepTiming
    {
        public CueStartMode startMode;
        public float delay;
        public float duration;   // 0 = held/looping (no natural end)
    }

    /// <summary>
    /// Compute each step's start time and the sequence's total duration.
    /// • <c>Immediate</c> → the effect's t=0 + delay (ignores previous).
    /// • <c>WithPrevious</c> → previous step's START + delay (concurrent / staggered).
    /// • <c>AfterPreviousCompletion</c> → previous step's END + delay (its END = start + duration; a held
    ///   step has duration 0, so its END == its START — the runner overrides this at runtime with the real stop).
    /// The first step ignores its mode and starts at its own delay. Negative start times clamp to 0.
    /// </summary>
    public static float[] Build(IReadOnlyList<StepTiming> steps, out float total)
    {
        int n = steps?.Count ?? 0;
        var starts = new float[n];
        total = 0f;

        float prevStart = 0f, prevEnd = 0f;

        for (int i = 0; i < n; i++)
        {
            var s = steps[i];
            float start;
            if (i == 0)
                start = s.delay;                                              // first step: effect t=0 + delay
            else if (s.startMode == CueStartMode.Immediate)
                start = s.delay;                                              // ignore previous
            else if (s.startMode == CueStartMode.WithPrevious)
                start = prevStart + s.delay;                                  // previous START
            else /* AfterPreviousCompletion */
                start = prevEnd + s.delay;                                    // previous END

            if (start < 0f) start = 0f;
            float dur = s.duration > 0f ? s.duration : 0f;
            float end = start + dur;

            starts[i] = start;
            if (end > total) total = end;

            prevStart = start;
            prevEnd = end;
        }

        return starts;
    }

    /// <summary>Total wall-clock length of the sequence (max step end time).</summary>
    public static float TotalDuration(IReadOnlyList<StepTiming> steps)
    {
        Build(steps, out var total);
        return total;
    }
}
