using System;
using System.Collections.Generic;

/// <summary>
/// Plays a <see cref="CueBookData"/> entry's element list over time. Fires each
/// <see cref="CueElement"/> through a play callback (→ <c>FxManager</c> element dispatch), retains its
/// <see cref="CueHandle"/>, and fires authored "cut" beats that Stop an earlier still-running element. Plain C#
/// and time-domain agnostic — FxManager feeds <see cref="Tick"/> the dt matching the book's timeMode (R10).
///
/// SCHEDULING (three start modes, resolved INCREMENTALLY at runtime):
///   • <c>Immediate</c> — start at the effect's t=0 + delay (ignores previous).
///   • <c>WithPrevious</c> — start at the previous element's START + delay.
///   • <c>AfterPreviousCompletion</c> — start when the previous element actually STOPS + delay. "Stops" =
///     its finite lifetime ends, OR a cut stops it, OR gameplay stops it — EVENT-DRIVEN, hence the runtime
///     resolver here rather than a single precompute. For all-finite books this matches <see cref="CueSchedule"/>
///     (the tested oracle); held/cut timing can only be known at runtime, which is why this class owns it.
///
/// VARIANTS (P18): consecutive <c>isVariant</c> elements form one group — Begin picks exactly one at random
/// (equal weights) and marks the rest skipped. Skipped members never fire and are transparent to scheduling
/// (successors resolve against the previous PLAYABLE element); cuts from/to skipped members are dropped.
///
/// A held (looping) element keeps the book alive (<see cref="HasLiveHeld"/>) so a gameplay-driven Stop can still
/// reach it. If <c>AfterPreviousCompletion</c> sits behind a held element that nothing ever stops, it never
/// fires (the author is warned — see CueBookLinter / the mode tooltip); the held element keeps the book alive
/// per its own contract until gameplay Stops the book.
/// </summary>
public class CueBookRunner
{
    private const float Unresolved = -1f;

    private readonly Func<CueElement, CueContext, CueHandle> _play;
    private readonly Action<CueHandle> _stop;

    private CueElement[] _elements = Array.Empty<CueElement>();
    private bool[] _skipped = Array.Empty<bool>();         // non-chosen variant-group members: never fire, transparent to scheduling
    private float[] _starts = Array.Empty<float>();        // resolved start time; Unresolved (-1) until known
    private float[] _naturalDur = Array.Empty<float>();    // finite schedule length; 0 = held (no natural end)
    private bool[] _fired = Array.Empty<bool>();
    private bool[] _stopped = Array.Empty<bool>();         // cut or gameplay stopped it (NOT natural end)
    private float[] _stopTime = Array.Empty<float>();      // clock at which a cut/gameplay stop happened
    private CueHandle[] _handles = Array.Empty<CueHandle>();

    private struct ScheduledCut { public int cutter; public int target; public float afterSeconds; public bool fired; }
    private readonly List<ScheduledCut> _cuts = new List<ScheduledCut>();

    private float _clock;
    private CueContext _ctx;

    public bool IsFinished { get; private set; } = true;

    /// <summary>Live element handles (None/stale entries included). FxManager walks these so a caller can
    /// reach a component on a spawned visual instance (FindOnInstance) — e.g. sync a MaterialRevealDriver
    /// to a gameplay windup.</summary>
    public IReadOnlyList<CueHandle> Handles => _handles;

    public CueBookRunner(Func<CueElement, CueContext, CueHandle> play, Action<CueHandle> stop)
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
    }

    /// <summary>True while any fired element is held (looping) and not yet cut/stopped — keeps the book
    /// alive so its handle stays valid for a gameplay Stop.</summary>
    public bool HasLiveHeld
    {
        get
        {
            for (int i = 0; i < _elements.Length; i++)
                if (_fired[i] && !_stopped[i] && _elements[i] != null && _elements[i].IsHeld())
                    return true;
            return false;
        }
    }

    public void Begin(IReadOnlyList<CueElement> elements, in CueContext ctx)
    {
        _ctx = ctx;
        _clock = 0f;
        _cuts.Clear();

        int n = elements?.Count ?? 0;
        if (n == 0)
        {
            _elements = Array.Empty<CueElement>();
            _skipped = Array.Empty<bool>();
            _starts = Array.Empty<float>();
            _naturalDur = Array.Empty<float>();
            _fired = Array.Empty<bool>();
            _stopped = Array.Empty<bool>();
            _stopTime = Array.Empty<float>();
            _handles = Array.Empty<CueHandle>();
            IsFinished = true;
            return;
        }

        _elements = new CueElement[n];
        _skipped = new bool[n];
        _starts = new float[n];
        _naturalDur = new float[n];
        _fired = new bool[n];
        _stopped = new bool[n];
        _stopTime = new float[n];
        _handles = new CueHandle[n];

        for (int i = 0; i < n; i++)
        {
            _elements[i] = elements[i];
            _starts[i] = Unresolved;
            _naturalDur[i] = elements[i]?.GetScheduleDuration() ?? 0f;
        }

        // Variant groups (P18): consecutive isVariant elements = one group; pick exactly one, skip the rest.
        // Skipped members never fire and are transparent to scheduling (successors chain off the chosen one).
        for (int i = 0; i < n; i++)
        {
            if (_elements[i] == null || !_elements[i].isVariant) continue;
            int end = i;
            while (end + 1 < n && _elements[end + 1] != null && _elements[end + 1].isVariant) end++;
            int chosen = i + UnityEngine.Random.Range(0, end - i + 1);   // equal weights v1 (spec)
            for (int v = i; v <= end; v++) _skipped[v] = v != chosen;
            i = end;
        }

        // First PLAYABLE element ignores its mode — it always starts at the effect's t=0 (+ its own delay).
        int first = 0;
        while (first < n && _skipped[first]) first++;
        if (first < n) _starts[first] = Math.Max(0f, _elements[first]?.startDelay ?? 0f);

        // Collect cuts (cut fire-time is resolved at runtime, since the cutter's start may be runtime-resolved).
        // Skipped cutters contribute nothing; cuts pointing at a skipped target are dropped (nothing to stop).
        for (int i = 0; i < n; i++)
        {
            var e = _elements[i];
            if (e == null || _skipped[i] || !e.canCut || e.cuts == null) continue;
            foreach (var cut in e.cuts)
            {
                if (cut.targetIndex < 0 || cut.targetIndex >= n) continue;   // out-of-range cut ignored (flagged by linter)
                if (_skipped[cut.targetIndex]) continue;                      // target is a non-chosen variant
                _cuts.Add(new ScheduledCut { cutter = i, target = cut.targetIndex,
                                             afterSeconds = cut.afterSeconds > 0f ? cut.afterSeconds : 0f, fired = false });
            }
        }

        IsFinished = false;
        Step();                                  // resolve + fire everything due at t=0
        RecomputeFinished();
    }

    public void Tick(float dt)
    {
        if (IsFinished) return;
        _clock += dt;
        Step();
        RecomputeFinished();
    }

    /// <summary>Resolve newly-resolvable starts, fire due elements, and run cuts — looped to a fixed point so a
    /// stop this frame (e.g. a cut) can immediately resolve an AfterPreviousCompletion successor in the same tick.</summary>
    private void Step()
    {
        int guard = _elements.Length + 1;        // bounded: each pass resolves at least one element or stops
        bool changed = true;
        while (changed && guard-- > 0)
        {
            changed = ResolveStarts();
            changed |= FireDue();
            changed |= ProcessCuts();
        }
    }

    // Previous PLAYABLE element (skipped variants are transparent to scheduling). -1 = none before i.
    private int PrevPlayable(int i)
    {
        for (int p = i - 1; p >= 0; p--)
            if (!_skipped[p]) return p;
        return -1;
    }

    // Resolve start times that have become knowable. Returns true if any element's start was newly resolved.
    private bool ResolveStarts()
    {
        bool any = false;
        for (int i = 1; i < _elements.Length; i++)
        {
            if (_skipped[i] || _starts[i] >= 0f) continue;        // skipped variant / already resolved
            var e = _elements[i];
            float delay = e != null ? Math.Max(0f, e.startDelay) : 0f;
            CueStartMode mode = e?.startMode ?? CueStartMode.WithPrevious;
            int prev = PrevPlayable(i);

            float resolved = Unresolved;
            if (mode == CueStartMode.Immediate || prev < 0)       // no playable predecessor → from the effect's t=0
            {
                resolved = delay;
            }
            else if (mode == CueStartMode.WithPrevious)
            {
                if (_starts[prev] >= 0f) resolved = _starts[prev] + delay;
            }
            else // AfterPreviousCompletion
            {
                if (IsDone(prev)) resolved = StopTimeOf(prev) + delay;
            }

            if (resolved >= 0f) { _starts[i] = resolved < 0f ? 0f : resolved; any = true; }
        }
        return any;
    }

    // Fire any element whose start is resolved and reached. Returns true if anything fired.
    private bool FireDue()
    {
        bool any = false;
        for (int i = 0; i < _elements.Length; i++)
        {
            if (_skipped[i] || _fired[i] || _starts[i] < 0f || _clock < _starts[i]) continue;
            _fired[i] = true;
            any = true;
            if (_elements[i] != null)
            {
                _handles[i] = _play(_elements[i], _ctx);
                if (_handles[i].IsNone) _stopped[i] = true;       // nothing live to hold or cut
            }
        }
        return any;
    }

    // Fire any due cut (cutter has started + afterSeconds elapsed), stopping its target. Returns true if any fired.
    private bool ProcessCuts()
    {
        bool any = false;
        for (int c = 0; c < _cuts.Count; c++)
        {
            var sc = _cuts[c];
            if (sc.fired || !_fired[sc.cutter]) continue;          // cutter not started yet
            float t = _starts[sc.cutter] + sc.afterSeconds;
            if (_clock < t) continue;
            if (!_fired[sc.target]) continue;                      // target not started yet — retry next pass
            sc.fired = true;
            _cuts[c] = sc;
            any = true;
            if (!_stopped[sc.target])
            {
                _stop(_handles[sc.target]);
                _stopped[sc.target] = true;
                _stopTime[sc.target] = _clock;
            }
        }
        return any;
    }

    // An element has "completed" for scheduling purposes: cut/gameplay-stopped, or a finite element past its end.
    private bool IsDone(int i)
    {
        if (!_fired[i]) return false;
        if (_stopped[i]) return true;
        return _naturalDur[i] > 0f && _clock >= _starts[i] + _naturalDur[i];
    }

    private float StopTimeOf(int i)
        => _stopped[i] ? _stopTime[i] : _starts[i] + _naturalDur[i];

    private void RecomputeFinished()
    {
        for (int i = 0; i < _elements.Length; i++)
        {
            if (_skipped[i]) continue;                            // non-chosen variant — never fires, never blocks
            if (_fired[i]) { if (!IsDone(i)) return; }            // still running (finite within lifetime, or held)
            else if (CanEventuallyFire(i)) return;                // an unfired element can still fire
        }
        IsFinished = true;
    }

    // Conservative (never declares finished early): can this unfired element still produce a future start?
    private bool CanEventuallyFire(int i)
    {
        if (_skipped[i] || _fired[i]) return false;
        int prev = PrevPlayable(i);
        if (prev < 0) return true;                                // first playable element always fires
        var mode = _elements[i]?.startMode ?? CueStartMode.WithPrevious;
        if (mode == CueStartMode.Immediate) return true;
        if (mode == CueStartMode.WithPrevious)
            return _starts[prev] >= 0f || CanEventuallyFire(prev);
        // AfterPreviousCompletion: pending while the predecessor is fired (running/done→resolves) or can still fire.
        return _fired[prev] || CanEventuallyFire(prev);
    }

    /// <summary>Stop every still-running element (gameplay Stop of the whole book; reset; scene unload).</summary>
    public void StopAll()
    {
        for (int i = 0; i < _elements.Length; i++)
        {
            if (_fired[i] && !_stopped[i])
            {
                _stop(_handles[i]);
                _stopped[i] = true;
                _stopTime[i] = _clock;
            }
        }
        IsFinished = true;
    }
}
