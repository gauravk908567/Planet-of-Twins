using System.Collections.Generic;

/// <summary>
/// Slot-recycling registry that version-stamps live instances. A handle whose
/// slot has since been reclaimed — and possibly reissued to a different instance — is INERT: the
/// canonical kill for the stale-pooled-reference bug class (the stale-StunVfx-child bug, F2). Plain
/// C# in <c>PlanetOfTwins.FxCore</c>, unit-tested in EditMode.
/// </summary>
public class CueInstanceRegistry<T>
{
    private struct Slot
    {
        public int version;
        public bool active;
        public T payload;
    }

    private readonly List<Slot> _slots = new List<Slot>();
    private readonly Stack<int> _free = new Stack<int>();

    public int ActiveCount { get; private set; }

    /// <summary>Take a slot (recycling a freed one if available) and return a fresh handle for it.</summary>
    public CueHandle Register(T payload)
    {
        int id;
        if (_free.Count > 0)
            id = _free.Pop();
        else
        {
            id = _slots.Count;
            _slots.Add(default);
        }

        var slot = _slots[id];
        slot.active = true;
        slot.payload = payload;
        _slots[id] = slot;
        ActiveCount++;
        return new CueHandle(id, slot.version);
    }

    public bool IsValid(CueHandle h)
        => h.Id >= 0 && h.Id < _slots.Count && _slots[h.Id].active && _slots[h.Id].version == h.Version;

    public bool TryGet(CueHandle h, out T payload)
    {
        if (IsValid(h)) { payload = _slots[h.Id].payload; return true; }
        payload = default;
        return false;
    }

    /// <summary>Free a slot. Bumps its version so the just-used handle (and any older one) is inert.</summary>
    public bool Reclaim(CueHandle h)
    {
        if (!IsValid(h)) return false;
        var slot = _slots[h.Id];
        slot.active = false;
        slot.payload = default;
        slot.version++;                 // generation bump → the handle that pointed here is now stale
        _slots[h.Id] = slot;
        _free.Push(h.Id);
        ActiveCount--;
        return true;
    }

    /// <summary>Snapshot of the currently-active (handle, payload) pairs into <paramref name="into"/>.</summary>
    public void GetActive(List<KeyValuePair<CueHandle, T>> into)
    {
        into.Clear();
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].active)
                into.Add(new KeyValuePair<CueHandle, T>(new CueHandle(i, _slots[i].version), _slots[i].payload));
    }
}
