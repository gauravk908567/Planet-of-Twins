/// <summary>
/// Interface so TutorialDirector never touches RescueEventController directly.
/// </summary>
public interface ITutorialRescueProvider
{
    RescueState CurrentRescueState { get; }

    /// <summary>
    /// True as soon as a twin is grabbed — fires before state leaves Idle.
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

    event System.Action<RescueState> OnRescueStateChanged;
}