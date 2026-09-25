public interface IRescueActive
{
    /// <summary>True when rescue state machine is running (state != Idle).
    /// Used by SoulConvergenceSystem to block F-hold during rescue.</summary>
    bool IsRescueActive { get; }

    /// <summary>True the moment a player is grabbed/dying (_activeTarget != null),
    /// even before state transitions out of Idle. Used by TeleportAbility gate �
    /// IsRescueActive caused a deadlock because state stays Idle until the soul
    /// physically arrives, blocking the very cast needed to start rescue.</summary>
    bool HasActiveRescueTarget { get; }

    /// <summary>The twin currently grabbed/dying (the rescue VICTIM), or null when none. The Weaver's Gate cast is
    /// gated on this: only the PARTNER may cast. If the victim casts its own gate it deploys the victim's soul onto
    /// the victim, and the partner can then never drive the rescue (couch two-soul model). Null-safe: null = no victim.</summary>
    Player ActiveGrabbedPlayer { get; }
}