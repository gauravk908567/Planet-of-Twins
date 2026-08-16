using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Couch co-op ownership + twin registry (Persistent, R3). This is the surviving half of
/// <c>TwinSelector</c>: the two twins ALWAYS exist and there is no "selected" twin, so the
/// selection state machine dies but the twin registry lives on here.
///
/// Two responsibilities, deliberately kept separate:
///  • <b>REGISTRY</b> — <see cref="TwinA"/>/<see cref="TwinB"/>/<see cref="Twins"/>/<see cref="Other"/>/
///    <see cref="Contains"/>: "give me the twins", selection-agnostic. Replaces every
///    <c>TwinSelector.Instance.LeftTwin/RightTwin</c> read (the 13 Role-1 consumers in the M1 map).
///  • <b>OWNERSHIP</b> — <see cref="For"/>/<see cref="SlotOf"/>/<see cref="Assign"/>: which local player
///    drives which twin. M1 hardcodes P1→TwinA, P2→TwinB; character select (M2) rewrites via
///    <see cref="Assign"/>. Feeds per-player input routing (device→slot→twin).
///
/// <para><b>M1.1 is ADDITIVE</b> — this coexists with <c>TwinSelector</c>; consumers migrate onto it in
/// M1.3. <c>TwinSelector</c>'s selection API (SelectedTransform/ForceSelect/lock) stays alive for Rescue
/// + Empower until M3; the physical <c>TwinSelector.cs</c> deletion is a post-M3 cleanup slice.</para>
///
/// R3 Restart-safe singleton (dup-destroy Awake guard, null Instance on OnDestroy, no DontDestroyOnLoad).
/// Lives on a GameObject in Persistent; the twin slots are same-scene serialized refs (R1) — the same two
/// <see cref="Player"/> objects <c>TwinSelector</c>'s left/right point at.
/// </summary>
[DisallowMultipleComponent]
public class PlayerRoster : MonoBehaviour
{
    public static PlayerRoster Instance { get; private set; }

    [Tooltip("The two twins — the same Player objects TwinSelector's left/right point at (same-scene, R1).")]
    [SerializeField] private Player twinA;
    [SerializeField] private Player twinB;

    // Ownership map: slot → twin. Seeded in Awake (M1 default), rewritten by character select (M2).
    private readonly Player[] _bySlot = new Player[2];

    // ── Registry (selection-agnostic) ──────────────────────────
    public Player TwinA => twinA;
    public Player TwinB => twinB;

    /// <summary>Both twins, in A→B order. Never null entries once Awake validated the slots.</summary>
    public IEnumerable<Player> Twins { get { yield return twinA; yield return twinB; } }

    /// <summary>The other twin, or null if <paramref name="twin"/> is not a roster twin.</summary>
    public Player Other(Player twin)
    {
        if (twin == twinA) return twinB;
        if (twin == twinB) return twinA;
        return null;
    }

    /// <summary>True if <paramref name="twin"/> is one of the two twins (excludes SoulPlayer, enemies).</summary>
    public bool Contains(Player twin) => twin != null && (twin == twinA || twin == twinB);

    // ── Ownership ──────────────────────────────────────────────
    /// <summary>The twin driven by <paramref name="slot"/> (P1/P2).</summary>
    public Player For(PlayerSlot slot) => _bySlot[(int)slot];

    /// <summary>Which slot drives <paramref name="twin"/>, or null if it isn't an owned twin.</summary>
    public PlayerSlot? SlotOf(Player twin)
    {
        if (_bySlot[(int)PlayerSlot.One] == twin) return PlayerSlot.One;
        if (_bySlot[(int)PlayerSlot.Two] == twin) return PlayerSlot.Two;
        return null;
    }

    /// <summary>Character-select (M2) entry point: bind a player slot to a twin.</summary>
    public void Assign(PlayerSlot slot, Player twin) => _bySlot[(int)slot] = twin;

    // ── Lifecycle (R3) ─────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (twinA == null || twinB == null)
        {
            Debug.LogError("[PlayerRoster] twinA/twinB unwired — assign both twins in the Inspector (R1). Disabling.", this);
            enabled = false;
            return;
        }

        // M1 default ownership — character select (M2) rewrites this via Assign().
        _bySlot[(int)PlayerSlot.One] = twinA;
        _bySlot[(int)PlayerSlot.Two] = twinB;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
