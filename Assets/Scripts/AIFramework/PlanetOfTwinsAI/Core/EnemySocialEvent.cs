/// <summary>
/// Social events that trigger mood transitions and VFX reactions.
/// Fired by game systems, received by EnemyMoodSystem.
/// </summary>
public enum EnemySocialEvent
{
    // ── Combat outcomes ────────────────────────────────────
    AllyGrabbedTwin,     // GroupGrab grabbed a twin — opportunistic spike
    TwinDowned,          // Twin brought to 0 HP — celebration
    TwinEscaped,         // Twin broke free from grab — frustration
    TwinAbilityUsed,     // Twin used stun/possess — alert reaction

    // ── Ally events ────────────────────────────────────────
    AllyDied,            // Nearby ally killed — mourning or anger
    AllyEnteredRage,     // Ally went into rage — contagion boost
    AllyRitualComplete,  // Witness summoned ally — triumph

    // ── Energy events ─────────────────────────────────────
    EnergyBurst,         // Faction energy hit 90+ — coordinated rally
    EnergyDepleted,      // Faction energy fell below 25 — caution

    // ── Investigation ─────────────────────────────────────
    SoundHeard,          // Sound event within range — curiosity
    TwinSpotted,         // First detection of twin — alert
    TwinLost,            // Twin left perception — search mode

    SpawnUnderAttack,   // spawn point is being attacked
    SpawnDestroyed,     // spawn point destroyed
    SpawnRespawned,     // spawn point came back online
    RitualStarted,      // Witness began ritual at site
    RitualCompleted,    // Witness completed ritual
}