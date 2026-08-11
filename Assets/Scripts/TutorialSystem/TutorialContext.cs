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

public class TutorialContext : MonoBehaviour
{
    public static TutorialContext Instance { get; private set; }

    public TutorialStage CurrentStage { get; private set; } = TutorialStage.None;

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

    public void SetStage(TutorialStage stage)
    {
        if (CurrentStage == stage) return;
        CurrentStage = stage;
        OnStageChanged?.Invoke(stage);
        Debug.Log($"[TutorialContext] Stage → {stage}");
    }
}