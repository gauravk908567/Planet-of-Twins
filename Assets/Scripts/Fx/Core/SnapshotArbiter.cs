using System.Collections.Generic;

/// <summary>
/// Highest-priority-wins arbiter for audio mixer snapshots. Deliberately
/// mirrors <c>TimeScaleService</c> (min-value-wins) so snapshot stomping gets the same request/
/// release cure as the seven timeScale writers. Plain C# in <c>PlanetOfTwins.FxCore</c> — the
/// winner logic is unit-tested without an AudioMixer. The owner of the FxCore assembly maps the
/// returned int id to an actual <c>AudioMixerSnapshot</c>.
/// </summary>
public class SnapshotArbiter
{
    /// <summary>The id returned when no owner is requesting anything (→ the Default snapshot).</summary>
    public const int DefaultSnapshot = -1;

    private struct Req { public int snapshotId; public int priority; }
    private readonly Dictionary<object, Req> _requests = new Dictionary<object, Req>();

    public int Count => _requests.Count;

    /// <summary>Register/replace <paramref name="owner"/>'s request. Returns true if the winner changed.</summary>
    public bool Request(object owner, int snapshotId, int priority, out int winner)
    {
        if (owner == null) { winner = CurrentWinner(); return false; }
        int before = CurrentWinner();
        _requests[owner] = new Req { snapshotId = snapshotId, priority = priority };
        winner = CurrentWinner();
        return winner != before;
    }

    /// <summary>Drop <paramref name="owner"/>'s request. Returns true if the winner changed.</summary>
    public bool Release(object owner, out int winner)
    {
        if (owner == null) { winner = CurrentWinner(); return false; }
        int before = CurrentWinner();
        _requests.Remove(owner);
        winner = CurrentWinner();
        return winner != before;
    }

    /// <summary>Highest-priority request's snapshot, or <see cref="DefaultSnapshot"/> if none.
    /// Ties keep the incumbent (strictly-greater replaces) — deterministic regardless of order.</summary>
    public int CurrentWinner()
    {
        bool any = false;
        int bestPriority = 0;
        int best = DefaultSnapshot;
        foreach (var r in _requests.Values)
        {
            if (!any || r.priority > bestPriority)
            {
                any = true;
                bestPriority = r.priority;
                best = r.snapshotId;
            }
        }
        return best;
    }
}
