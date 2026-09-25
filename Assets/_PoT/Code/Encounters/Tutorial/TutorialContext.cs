using UnityEngine;

/// <summary>
/// Global tutorial state. Singleton — always active in scene.
///
/// Every system that behaves differently during tutorial subscribes to
/// OnStageChanged or polls CurrentStage.
///
/// USAGE:
///   TutorialContext.Instance.SetStage(TutorialStage.RescueIntro);
///   if (TutorialContext.Instance.IsTutorialActive) { ... }
/// </summary>
public enum TutorialStage
{
    None,
    MovementOnly,       // was Movement
    SwitchUnlocked,     // was SwitchIntro
    GateOpen,           // new
    SharedHealth,       // was BothApart — this is the health teaching phase
    RescueIntro,
    MeleeIntro,      // ← NEW — player picks up sword, learns attack
    AbilityIntro,
    Complete      // tutorial done — everything normal
}

public class TutorialContext : MonoBehaviour, WorldFlagRegistry.IWorldFlagObject
{
    public static TutorialContext Instance { get; private set; }

    public TutorialStage CurrentStage { get; private set; } = TutorialStage.None;

    /// <summary>World flag recording that this area's tutorial is finished (Unlock-All step, Continue or dev skip).
    /// It is written into the save slot, so a finished tutorial never replays: TutorialDirector skips on it, and
    /// TutorialZoneTrigger ignores a Complete tutorial (BUG-133).</summary>
    public string CompletionFlagKey => "tutorial:" + gameObject.scene.name;

    /// <summary>True when this save/session already finished the tutorial (e.g. L1 streamed out and back in).</summary>
    public bool IsCompletedInSave =>
        WorldFlagRegistry.Instance != null && WorldFlagRegistry.Instance.IsSet(CompletionFlagKey);

    /// <summary>True while tutorial is running (not None and not Complete).</summary>
    public bool IsTutorialActive =>
        CurrentStage != TutorialStage.None &&
        CurrentStage != TutorialStage.Complete;

    /// <summary>True specifically during rescue teaching — systems use this to slow timers.</summary>
    public bool IsRescueTutorial => CurrentStage == TutorialStage.RescueIntro;

    public event System.Action<TutorialStage> OnStageChanged;

    [Header("Timer scale during rescue tutorial (0.25 = 25% speed)")]
    [SerializeField] private float rescueTimerScale = 0.25f;
    public float RescueTimerScale => rescueTimerScale;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // R5: self-register with the Persistent registry so a restore re-asserts the completion flag.
    private void OnEnable()
    {
        if (WorldFlagRegistry.Instance != null) WorldFlagRegistry.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (WorldFlagRegistry.Instance != null) WorldFlagRegistry.Instance.Unregister(this);
    }

    public void SetStage(TutorialStage stage)
    {
        if (CurrentStage == stage) return;
        CurrentStage = stage;
        if (stage == TutorialStage.Complete) MarkCompleteFlag();
        OnStageChanged?.Invoke(stage);
        Debug.Log($"[TutorialContext] Stage → {stage}");
    }

    /// <summary>WorldFlagRegistry restore hook. A respawn can hand back an OLDER flag set (a checkpoint saved before the
    /// tutorial ended); a finished tutorial re-asserts its flag so the next checkpoint doesn't save it as unfinished
    /// (same invariant as GateActivatable).</summary>
    public void ApplyWorldFlags()
    {
        if (CurrentStage == TutorialStage.Complete) MarkCompleteFlag();
    }

    private void MarkCompleteFlag()
    {
        if (WorldFlagRegistry.Instance != null) WorldFlagRegistry.Instance.Set(CompletionFlagKey);
    }
}