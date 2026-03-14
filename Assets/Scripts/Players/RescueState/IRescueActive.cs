public interface IRescueActive
{
    /// <summary>True when rescue state machine is running (state != Idle).
    /// Used by SoulConvergenceSystem to block F-hold during rescue.</summary>
    bool IsRescueActive { get; }

    /// <summary>True the moment a player is grabbed/dying (_activeTarget != null),
    /// even before state transitions out of Idle. Used by TeleportAbility gate —
    /// IsRescueActive caused a deadlock because state stays Idle until the soul
    /// physically arrives, blocking the very cast needed to start rescue.</summary>
    bool HasActiveRescueTarget { get; }
}