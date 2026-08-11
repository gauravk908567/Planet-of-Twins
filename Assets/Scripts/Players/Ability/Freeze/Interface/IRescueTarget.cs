using System;
using UnityEngine;

public interface IRescueTarget
{
    // ── Data ──────────────────────────────────────────────────
    Transform GrabbedPlayerTransform { get; }
    Player GrabbedPlayer { get; }

    /// <summary>0 = dead, 1 = full time remaining.</summary>
    float NormalisedTTK { get; }

    /// <summary>Button presses per second required to fill the rescue bar.</summary>
    float MashFrequency { get; }

    /// <summary>How long the rescuer has to fill the bar (seconds).</summary>
    float MashWindowDuration { get; }

    /// <summary>Cooldown after a failed mash before retry is allowed.</summary>
    float MashCooldown { get; }

    /// <summary>HP restored to trapped player on successful rescue.</summary>
    float PartialHealAmount { get; }

    /// <summary>Tier 1 = true (can struggle), Tier 2 = false (fully frozen).</summary>
    bool CanGrabbedPlayerStruggle { get; }
    /// <summary>Seconds TTK is paused per struggle mash. Used by RescueEventController for 30% cap tracking.</summary>
    float StrugglePauseDuration { get; }

    // ── Events ────────────────────────────────────────────────
    event Action<Player> OnPlayerGrabbed;
    event Action OnPlayerReleased;
    event Action OnPlayerKilled;

    // ── Timer control (called only by RescueEventController) ──
    void PauseTTK();
    void ResumeTTK();
    void ReleasePlayer(float healAmount);
    void KillPlayer();
    void OnStruggle(); // called when grabbed player mashes attack (tier-1 trap only)
}