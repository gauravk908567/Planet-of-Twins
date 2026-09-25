/// <summary>
/// Couch M2 — the boot-scoped handoff from the <b>FrontEnd</b> scene to <b>Persistent</b>. The front-end
/// (Main Menu → Save Slot → Character Select) now runs in its own scene, BEFORE Persistent loads, so it can no
/// longer write <see cref="PlayerRoster"/> or read the game's managers directly. Instead it records its outcome
/// here; Persistent applies it on load (<see cref="PlayerRoster"/> reads the twin choice, the Continue boot path
/// reads the mode/slot).
///
/// <para><b>What crosses the boundary is tiny and pure data</b> — the twin choice is one bit (does slot One get
/// Lyra), plus the New Game/Continue mode and chosen save slot. Device→slot pairing does NOT cross:
/// <see cref="CouchDeviceManager.AssignAuto"/> is deterministic (controller[0]→P1, controller[1]→P2), so
/// Persistent's own CouchDeviceManager re-derives the identical pairing from the same connected devices.</para>
///
/// <para>Plain static (NOT a ScriptableObject — R7 forbids SO runtime state; NOT a singleton holding
/// player-scoped state — this is boot config, not gameplay state). Statics survive a Restart in builds, which is
/// harmless here: the front-end always runs on boot and rewrites this, and <see cref="GameBootstrapper"/> calls
/// <see cref="Clear"/> at the start of every boot so no stale selection leaks in.</para>
/// </summary>
public static class SessionSetup
{
    public enum BootMode { NewGame, Continue }

    /// <summary>True once Character Select has recorded a twin choice this boot. Persistent applies it only then;
    /// absent data = fall back to the M1 default ownership (P1→TwinA/Lyra, P2→TwinB/Kai).</summary>
    public static bool HasSelection { get; private set; }

    /// <summary>Slot One → Lyra (TwinA) when true, else Kai (TwinB). Slot Two always gets the other twin.</summary>
    public static bool P1GetsLyra { get; private set; } = true;

    /// <summary>New Game (→ intro cutscene) or Continue (→ load the saved area directly).</summary>
    public static BootMode Mode { get; private set; } = BootMode.NewGame;

    /// <summary>The save slot the player chose on the slot screen (-1 = none / not chosen).</summary>
    public static int SaveSlot { get; private set; } = -1;

    /// <summary>Character Select records its resolved choice (called by <see cref="CharacterSelectController"/>).</summary>
    public static void SetSelection(bool p1GetsLyra)
    {
        P1GetsLyra = p1GetsLyra;
        HasSelection = true;
    }

    /// <summary>The save-slot screen records the boot mode + slot.</summary>
    public static void SetMode(BootMode mode, int saveSlot)
    {
        Mode = mode;
        SaveSlot = saveSlot;
    }

    /// <summary>Reset to defaults — called by <see cref="GameBootstrapper"/> at the start of every boot so a prior
    /// session's choice (statics survive Restart in builds) never leaks into a fresh run.</summary>
    public static void Clear()
    {
        HasSelection = false;
        P1GetsLyra = true;
        Mode = BootMode.NewGame;
        SaveSlot = -1;
    }
}
