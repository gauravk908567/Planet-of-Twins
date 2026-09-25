/// <summary>
/// Enum for mood VFX reactions � eliminates string mismatch errors.
/// Referenced by MoodTransitionRule.vfxTag (serialized data). NOTE: the consumer
/// EnemyVFXController.PlayMoodReaction is retired — mood VFX is now Manpu-driven (the mood transition
/// itself fires the glyph + aura). This enum + the vfxTag field are now dead data kept only to avoid
/// churning every MoodTransitionProfile asset; drop them in a later GUID-safe cleanup.
/// </summary>
public enum MoodVFXTag
{
    None,
    GrimSatisfaction,   // Opportunistic � dark grin
    Frustrated,         // Frustrated � anger flash
    Grief,              // Grieving � loss reaction
    Rage,               // Enraged / Aggressive
    Fear,               // Wounded / Cautious
    Panic,              // Panicked
    Contempt,           // Contemptuous � Penitent mockery
    Rally,              // Energy burst � group rallying
    DarkEnergy,         // Dark energy threshold reached
}