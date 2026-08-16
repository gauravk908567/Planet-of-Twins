using UnityEngine;

/// <summary>
/// Persistent seam that resolves which <see cref="IInputProvider"/> drives each twin
/// (see <see cref="IPlayerInputRouter"/>). M0 stage: one shared reader drives everything, so this
/// is behaviour-neutral — it exists so the per-player split (M1) is a one-file change.
///
/// Wiring: lives on a GameObject in Persistent.unity. Leave <see cref="_inputProviderObject"/>
/// pointing at the same-scene TwinInputReader (R1). If left blank it falls back to
/// <c>TwinInputReader.Instance</c> so the seam still works before the slot is wired.
///
/// Persistence = living in Persistent (R3) — the duplicate-destroy Awake guard + null on OnDestroy
/// is the standard Restart-safe pattern; NO DontDestroyOnLoad.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInputRouter : MonoBehaviour, IPlayerInputRouter
{
    public static PlayerInputRouter Instance { get; private set; }

    [Tooltip("Same-scene TwinInputReader (Persistent, R1). M0 routes BOTH twins to this one reader. " +
             "Blank = fall back to TwinInputReader.Instance.")]
    [SerializeField] private MonoBehaviour _inputProviderObject;   // → IInputProvider

    private IInputProvider _shared;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Lazy resolve (never in Awake — TwinInputReader's Instance may not be set yet, R8 ordering).
    private IInputProvider Resolve()
    {
        if (_shared != null) return _shared;
        _shared = (_inputProviderObject as IInputProvider) ?? TwinInputReader.Instance;
        if (_shared == null)
            Debug.LogError("[PlayerInputRouter] No IInputProvider — wire the Persistent TwinInputReader " +
                           "into _inputProviderObject (or ensure TwinInputReader.Instance exists).", this);
        return _shared;
    }

    // ── IPlayerInputRouter ─────────────────────────────────────
    public IInputProvider Shared => Resolve();

    /// <summary>M0: every twin is driven by the one shared reader. M1 swaps this to device-bound providers.</summary>
    public IInputProvider ProviderFor(Player twin) => Resolve();

    // ── Static convenience for consumers (lazy, Instance-order-safe) ──
    /// <summary>Shared-UI input (pause/skilltree/overview/intro/QTE/hints). Falls back to
    /// TwinInputReader.Instance if the router GameObject isn't in the scene yet.</summary>
    public static IInputProvider SharedInput =>
        Instance != null ? Instance.Shared : TwinInputReader.Instance;

    /// <summary>Per-twin gameplay input. M0 == SharedInput; M1 == the twin's owning device.</summary>
    public static IInputProvider For(Player twin) =>
        Instance != null ? Instance.ProviderFor(twin) : TwinInputReader.Instance;
}
