/// <summary>
/// The two main clans of the planet.
/// Luminari — light, peace, growth, healing (Lyra's clan)
/// Vethara  — dark, energy, mystery, coiled power (Kai's clan)
/// </summary>
public enum ClanAlignment
{
    Luminari,
    Vethara,
}

/// <summary>
/// Allied clan types — 9 smaller clans aligned to one of the two main clans.
/// Each maps to one enemy type. Names are placeholders — update with final lore names.
///
/// Any — special value for fallback ProximityPowerEntry entries.
///       Skips clan check — fires with any compatible partner present.
///
/// Luminari-aligned (light, community, healing, growth):
///   Veran    → Witness
///   Sorath   → Summoner
///   Aelun    → Siphon
///   Kethori  → GroupGrab
///
/// Vethara-aligned (dark, soldier, coiled power, energy):
///   Durvak   → TetherBreaker
///   Vorrn    → Severed
///   Grauth   → BasicMelee
///   Zelvari  → Ranged
///   Korreth  → Penitent
/// </summary>
public enum AlliedClanType
{
    // Special
    Any,          // Fallback — matches any partner clan type

    // Luminari-aligned
    Witness,      // Witness — truth-seers, observers, corrupted healers
    Summoner,     // Summoner — seed-growers, cultivators, corrupted into weapon-farmers
    Siphon,       // Siphon — breath/soul cleansers, spiritual healers, corrupted into drainers
    GroupGrab,    // GroupGrab — root-binders, community-keepers, corrupted into mob-draggers

    // Vethara-aligned
    TetherBreaker, // TetherBreaker — chain-weavers, coil-smiths, binding power weaponised
    Severed,       // Severed — iron-grievers, soldier-mourners, death-bond warriors
    Melee,         // BasicMelee — stone-fists, raw force, simplest dark expression
    Ranged,        // Ranged — thread-reachers, energy projectors, precision-strikers
    Penitent,      // Penitent — crushers, elite warriors, pride as armour
}