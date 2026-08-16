using UnityEngine;

/// <summary>
/// Persistent seam that resolves which <see cref="IInputProvider"/> drives each twin
/// (see <see cref="IPlayerInputRouter"/>). Sits ABOVE <see cref="TwinInputReader"/> — it does NOT replace
/// the reader; it routes providers to twins.
///
/// <para><b>M1.6 — device-aware:</b> two provider slots, one per <see cref="PlayerSlot"/>:
/// <see cref="_inputProviderObject"/> = P1 (slot One), <see cref="_inputProviderObjectP2"/> = P2 (slot Two,
/// OPTIONAL). <see cref="ProviderFor"/> looks up the twin's owning slot via <see cref="PlayerRoster"/> and
/// returns that slot's provider. If P2's slot is empty (no second device wired yet), P2 falls back to P1 —
/// so single-device play still works and both twins read the same input until the second device is paired.</para>
///
/// Wiring: lives on the PlayerManager hub in Persistent. P1 slot → the same-scene <see cref="TwinInputReader"/>
/// (R1). Leave P2 blank until a second input provider (e.g. a gamepad-bound reader) is authored.
///
/// Persistence = living in Persistent (R3) — duplicate-destroy Awake guard + null on OnDestroy, no DDOL.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInputRouter : MonoBehaviour, IPlayerInputRouter
{
    public static PlayerInputRouter Instance { get; private set; }

    [Tooltip("P1 / slot One provider — the same-scene TwinInputReader (Persistent, R1). " +
             "Blank = fall back to TwinInputReader.Instance.")]
    [SerializeField] private MonoBehaviour _inputProviderObject;     // → IInputProvider (P1)

    [Tooltip("P2 / slot Two provider (OPTIONAL) — a second input source for couch co-op. " +
             "Blank = P2 falls back to P1 (single-device play).")]
    [SerializeField] private MonoBehaviour _inputProviderObjectP2;   // → IInputProvider (P2, optional)

    private IInputProvider _p1;
    private IInputProvider _p2;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Lazy resolve (never in Awake — TwinInputReader.Instance may not be set yet, R8 ordering).
    private IInputProvider P1
    {
        get
        {
            if (_p1 != null) return _p1;
            _p1 = (_inputProviderObject as IInputProvider) ?? TwinInputReader.Instance;
            if (_p1 == null)
                Debug.LogError("[PlayerInputRouter] No P1 IInputProvider — wire the Persistent TwinInputReader " +
                               "into _inputProviderObject (or ensure TwinInputReader.Instance exists).", this);
            return _p1;
        }
    }

    // P2 is optional — falls back to P1 when no second device is wired (single-device play).
    private IInputProvider P2 => _p2 ??= (_inputProviderObjectP2 as IInputProvider) ?? P1;

    // ── IPlayerInputRouter ─────────────────────────────────────
    /// <summary>Shared-UI input (pause / skill tree / overview / intro / QTE / hints). Uses P1 for now;
    /// a future any-of aggregator (either player drives shared UI) is a follow-up.</summary>
    public IInputProvider Shared => P1;

    /// <summary>The provider that drives <paramref name="twin"/>, by its owning <see cref="PlayerSlot"/>.
    /// Slot Two → P2 (falls back to P1 if unwired); everything else → P1.</summary>
    public IInputProvider ProviderFor(Player twin)
    {
        var slot = PlayerRoster.Instance != null ? PlayerRoster.Instance.SlotOf(twin) : null;
        return slot == PlayerSlot.Two ? P2 : P1;
    }

    // ── Static convenience for consumers (lazy, Instance-order-safe) ──
    /// <summary>Shared-UI input. Falls back to TwinInputReader.Instance if the router isn't in the scene yet.</summary>
    public static IInputProvider SharedInput =>
        Instance != null ? Instance.Shared : TwinInputReader.Instance;

    /// <summary>Per-twin gameplay input, routed by owning player slot (M1.6). Falls back to
    /// TwinInputReader.Instance if the router isn't in the scene yet.</summary>
    public static IInputProvider For(Player twin) =>
        Instance != null ? Instance.ProviderFor(twin) : TwinInputReader.Instance;
}
