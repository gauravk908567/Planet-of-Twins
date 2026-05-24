using System;
using UnityEngine;

/// <summary>
/// Global event bus for mood-affecting social events.
/// Enemies subscribe to relevant events via EnemyMoodSystem.
/// Fired by game systems: GroupGrabEnemy, Health, FactionEnergySystem etc.
///
/// USAGE — firing:
///   MoodEventBus.Fire(EnemySocialEvent.AllyGrabbedTwin, grabbedPlayer.gameObject);
///
/// USAGE — listening (done by EnemyMoodSystem, not manually):
///   MoodEventBus.OnSocialEvent += handler;
/// </summary>
public static class MoodEventBus
{
    /// <summary>
    /// Fired whenever a social event occurs.
    /// source = the GameObject that caused it (grabbed twin, dead ally, etc.)
    /// Can be null for global events like EnergyBurst.
    /// </summary>
    public static event Action<EnemySocialEvent, GameObject> OnSocialEvent;

    public static void Fire(EnemySocialEvent evt, GameObject source = null)
    {
        OnSocialEvent?.Invoke(evt, source);
    }

    // ── Convenience methods ────────────────────────────────
    public static void AllyGrabbedTwin(GameObject grabbedTwin)
        => Fire(EnemySocialEvent.AllyGrabbedTwin, grabbedTwin);

    public static void TwinDowned(GameObject twin)
        => Fire(EnemySocialEvent.TwinDowned, twin);

    public static void TwinEscaped(GameObject twin)
        => Fire(EnemySocialEvent.TwinEscaped, twin);

    public static void AllyDied(GameObject deadAlly)
        => Fire(EnemySocialEvent.AllyDied, deadAlly);

    public static void AllyEnteredRage(GameObject ragingAlly)
        => Fire(EnemySocialEvent.AllyEnteredRage, ragingAlly);

    public static void EnergyBurst()
        => Fire(EnemySocialEvent.EnergyBurst, null);

    public static void EnergyDepleted()
        => Fire(EnemySocialEvent.EnergyDepleted, null);

    public static void SoundHeard(GameObject soundSource)
        => Fire(EnemySocialEvent.SoundHeard, soundSource);

    public static void TwinSpotted(GameObject twin)
        => Fire(EnemySocialEvent.TwinSpotted, twin);

    public static void TwinLost()
        => Fire(EnemySocialEvent.TwinLost, null);
}