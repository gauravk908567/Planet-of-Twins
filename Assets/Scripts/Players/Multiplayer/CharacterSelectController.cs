using System;
using UnityEngine;

/// <summary>
/// Couch M2 — the pre-game character-select state machine. Each local player (slot) picks
/// <see cref="CharacterPick.Lyra"/> / <see cref="CharacterPick.Kai"/> / <see cref="CharacterPick.Random"/>
/// and readies up; when both are ready AND resolvable to two DISTINCT twins, the controller writes the
/// ownership into <see cref="PlayerRoster"/> via <see cref="PlayerRoster.Assign"/> and raises
/// <see cref="OnSelectionComplete"/>. This is the only place that rewrites the M1 default ownership.
///
/// <para><b>Rules (as specced):</b> both slots default to <c>Random</c>; a player can change pick or
/// <b>Back out</b> of ready (<see cref="SetReady"/>(slot, false)); the game will NOT start until both
/// are ready; if both ready on the <b>same explicit twin</b> it is NOT startable (Random always resolves
/// to the distinct one; both-Random → coin-flip distinct). Mode-agnostic — couch drives it with two
/// local devices, the (later) online lobby drives the same screen at host time.</para>
///
/// <para><b>Design:</b> a plain MonoBehaviour holding selection state, NOT a singleton — the select
/// screen holds a serialized reference to it (same-scene, R1). It does not poll input or draw UI: the
/// screen reads <see cref="PickOf"/>/<see cref="IsReady"/>/<see cref="CanStart"/> and calls
/// <see cref="SetPick"/>/<see cref="Cycle"/>/<see cref="SetReady"/>; a menu/bootstrapper waits on
/// <see cref="OnSelectionComplete"/>. The pure resolution logic is exposed as the static
/// <see cref="TryResolve"/> so it is headless-testable (see the Couch self-test menu item).</para>
///
/// <para><b>Contract:</b> <see cref="PlayerRoster"/> must exist (Persistent loaded) by the time
/// selection completes — finalize logs an error and no-ops if it is null.</para>
/// </summary>
[DisallowMultipleComponent]
public class CharacterSelectController : MonoBehaviour
{
    // Per-slot state (index by (int)PlayerSlot). Field-initialized so it's valid even before Awake.
    private readonly CharacterPick[] _pick  = { CharacterPick.Random, CharacterPick.Random };
    private readonly bool[]          _ready = { false, false };

    /// <summary>True once both picks are resolved and written into the roster (state is then locked).</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Raised on any state change (pick / ready / complete) so the UI can refresh.</summary>
    public event Action OnChanged;

    /// <summary>Raised once, when both players are ready and ownership has been written to the roster.</summary>
    public event Action OnSelectionComplete;

    // ── Queries (UI reads these) ───────────────────────────────
    public CharacterPick PickOf(PlayerSlot slot) => _pick[(int)slot];
    public bool IsReady(PlayerSlot slot)         => _ready[(int)slot];

    /// <summary>Both ready AND resolvable to two distinct twins — i.e. pressing "start" is legal.</summary>
    public bool CanStart => _ready[0] && _ready[1] &&
                            TryResolve(_pick[0], _pick[1], true, out _, out _);

    /// <summary>Both ready but on the SAME explicit twin — the "won't start" conflict the UI should flag.</summary>
    public bool HasConflict => _ready[0] && _ready[1] &&
                               !TryResolve(_pick[0], _pick[1], true, out _, out _);

    // ── Commands (UI/input calls these) ────────────────────────
    /// <summary>Set a slot's pick. Ignored while that slot is readied (Back out first) or once complete.</summary>
    public void SetPick(PlayerSlot slot, CharacterPick pick)
    {
        if (IsComplete) return;
        int i = (int)slot;
        if (_ready[i] || _pick[i] == pick) return;
        _pick[i] = pick;
        OnChanged?.Invoke();
    }

    /// <summary>Cycle a slot's pick Lyra → Kai → Random (dir +1) or reverse (dir -1).</summary>
    public void Cycle(PlayerSlot slot, int dir)
    {
        if (IsComplete) return;
        int i = (int)slot;
        if (_ready[i]) return;
        const int n = 3;
        int cur = (((int)_pick[i] + dir) % n + n) % n;
        SetPick(slot, (CharacterPick)cur);
    }

    /// <summary>Ready-up (Select) or back out (Back). Readying may finalize if both slots are then ready.</summary>
    public void SetReady(PlayerSlot slot, bool ready)
    {
        if (IsComplete) return;
        int i = (int)slot;
        if (_ready[i] == ready) return;
        _ready[i] = ready;
        OnChanged?.Invoke();
        if (ready) TryFinalize();
    }

    /// <summary>Clear back to the initial state (both Random, not ready) — call when (re)entering the screen.</summary>
    public void ResetSelection()
    {
        _pick[0]  = _pick[1]  = CharacterPick.Random;
        _ready[0] = _ready[1] = false;
        IsComplete = false;
        OnChanged?.Invoke();
    }

    // ── Finalize ───────────────────────────────────────────────
    private void TryFinalize()
    {
        if (IsComplete || !_ready[0] || !_ready[1]) return;

        bool coinPrefersLyraForP1 = UnityEngine.Random.value < 0.5f;
        if (!TryResolve(_pick[0], _pick[1], coinPrefersLyraForP1, out var r1, out var r2))
        {
            // Both ready on the same explicit twin — do NOT start; UI shows the conflict (HasConflict).
            OnChanged?.Invoke();
            return;
        }

        var roster = PlayerRoster.Instance;
        if (roster == null)
        {
            Debug.LogError("[CharacterSelectController] PlayerRoster.Instance is null at finalize — cannot " +
                           "assign ownership. Persistent must be loaded before character select completes.", this);
            return;
        }

        roster.Assign(PlayerSlot.One, PlayerFor(r1, roster));
        roster.Assign(PlayerSlot.Two, PlayerFor(r2, roster));
        IsComplete = true;
        OnChanged?.Invoke();
        OnSelectionComplete?.Invoke();
    }

    // Lyra → TwinA (left/Luminari), Kai → TwinB (right/Vethara). r1/r2 are always explicit here.
    private static Player PlayerFor(CharacterPick c, PlayerRoster roster)
        => c == CharacterPick.Kai ? roster.TwinB : roster.TwinA;

    // ── Pure resolution (headless-testable) ────────────────────
    /// <summary>
    /// Resolve two picks to a DISTINCT explicit twin each (never <see cref="CharacterPick.Random"/>).
    /// Returns <c>false</c> — leaving the picks unresolved — when both are the SAME explicit twin, which
    /// is the "won't start" case. <paramref name="coinPrefersLyraForP1"/> only matters when BOTH are Random.
    /// </summary>
    public static bool TryResolve(CharacterPick p1, CharacterPick p2, bool coinPrefersLyraForP1,
                                  out CharacterPick r1, out CharacterPick r2)
    {
        bool p1Random = p1 == CharacterPick.Random;
        bool p2Random = p2 == CharacterPick.Random;

        if (!p1Random && !p2Random)
        {
            r1 = p1; r2 = p2;
            return p1 != p2;                 // same explicit twin → not startable
        }
        if (p1Random && p2Random)
        {
            r1 = coinPrefersLyraForP1 ? CharacterPick.Lyra : CharacterPick.Kai;
            r2 = OtherExplicit(r1);
            return true;
        }
        if (p1Random)                        // p2 explicit
        {
            r2 = p2; r1 = OtherExplicit(p2);
            return true;
        }
        // p2 random, p1 explicit
        r1 = p1; r2 = OtherExplicit(p1);
        return true;
    }

    private static CharacterPick OtherExplicit(CharacterPick c)
        => c == CharacterPick.Kai ? CharacterPick.Lyra : CharacterPick.Kai;
}
