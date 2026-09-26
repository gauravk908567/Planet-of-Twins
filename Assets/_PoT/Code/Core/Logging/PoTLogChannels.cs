/// <summary>
/// Planet of Twins' detailed log channels, as a mask (DevConfig in the Editor / Development builds). Each name is
/// also the channel's registry name and the tag its lines carry. The old <c>[Tag]</c> prefix of a Debug.Log picks
/// its channel in the log sweep (game.md §27.7 phase 5).
/// </summary>
[System.Flags]
public enum PoTLogChannels
{
    None      = 0,
    /// <summary>Enemy brains + ecology: mood, dark energy, bonds, pacts/combos, perception, POIs, commanders.</summary>
    AI        = 1 << 0,
    /// <summary>Spawner, spawn zones and points, the enemy pool.</summary>
    Spawn     = 1 << 1,
    /// <summary>Boot, front-end hand-off, intro, area streaming, scene triggers.</summary>
    Streaming = 1 << 2,
    /// <summary>Save slots, checkpoints, Continue, soft reset.</summary>
    Save      = 1 << 3,
    /// <summary>Player abilities and attacks, projectiles, damage and health.</summary>
    Combat    = 1 << 4,
    /// <summary>Twin systems: shared health/bond, rescue, Accord, joint powers, death proxy.</summary>
    Twins     = 1 << 5,
    /// <summary>QTE manager, anchors, zones, enemy freeze.</summary>
    QTE       = 1 << 6,
    /// <summary>Tutorial director, steps, tutorial traps.</summary>
    Tutorial  = 1 << 7,
    /// <summary>Devices, couch pairing, roster, controller database.</summary>
    Input     = 1 << 8,
    /// <summary>Menus, HUD, skill tree, localization.</summary>
    UI        = 1 << 9,
    /// <summary>Cues, VFX, audio, music, camera cues.</summary>
    Fx        = 1 << 10,
    /// <summary>World objects: traps, gates, interactables, pickups, world ambience.</summary>
    World     = 1 << 11,
}
