/// <summary>
/// Interface so TutorialDirector never touches RescueEventController directly.
/// </summary>
public interface ITutorialRescueProvider
{
    RescueState CurrentRescueState { get; }

    /// <summary>
    /// True as soon as a twin is grabbed � fires before state leaves Idle.
    /// </summary>
    bool HasActiveRescueTarget { get; }

    /// <summary>
    /// Latches true the moment rescue succeeds and stays true.
    /// Use this instead of checking CurrentRescueState == Success because
    /// RescueEventController resets state to Idle immediately after success,
    /// making it impossible to catch via polling.
    /// Reset via ResetSuccessFlag() before starting a new rescue watch.
    /// </summary>
    bool WasSuccessful { get; }

    /// <summary>Reset the success latch before watching a new rescue.</summary>
    void ResetSuccessFlag();

    /// <summary>
    /// Latches true the moment rescue FAILS and stays true (mirror of WasSuccessful). Poll this instead of
    /// CurrentRescueState == Failed: the controller resets state to Idle synchronously on failure, so a
    /// frame-poll never catches Failed. Reset via ResetFailFlag().
    /// </summary>
    bool WasFailed { get; }

    /// <summary>Reset the failure latch before watching a new rescue / after handling a failure.</summary>
    void ResetFailFlag();

    /// <summary>
    /// Fully restore both twins (alive, HP max, drain reset, unfrozen, un-grabbed) so a failed tutorial
    /// rescue can be retried — the tutorial trap kills for real, so the reset must also revive.
    /// </summary>
    void ReviveTwinsForRetry();

    /// <summary>
    /// While true, a Failed rescue does NOT fire the game-over signal (OnRescueFailed). The Failed
    /// state transition and CurrentRescueState still happen — only the game-over trigger is gated.
    /// The tutorial rescue-watch step sets this while it owns the rescue so a failed tutorial rescue
    /// drives its own fade → reset → retry instead of "battle lost" (BUG-103). Non-tutorial rescues
    /// leave it false → a failed rescue is game-over as designed.
    /// </summary>
    bool SuppressFailGameOver { get; set; }

    event System.Action<RescueState> OnRescueStateChanged;
}