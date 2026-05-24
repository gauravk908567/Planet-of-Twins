/// <summary>
/// Enum for mood VFX reactions — eliminates string mismatch errors.
/// Used by MoodTransitionRule.vfxTag and EnemyVFXController.PlayMoodReaction.
/// </summary>
public enum MoodVFXTag
{
    None,
    GrimSatisfaction,   // Opportunistic — dark grin
    Frustrated,         // Frustrated — anger flash
    Grief,              // Grieving — loss reaction
    Rage,               // Enraged / Aggressive
    Fear,               // Wounded / Cautious
    Panic,              // Panicked
    Contempt,           // Contemptuous — Penitent mockery
    Rally,              // Energy burst — group rallying
    DarkEnergy,         // Dark energy threshold reached
}