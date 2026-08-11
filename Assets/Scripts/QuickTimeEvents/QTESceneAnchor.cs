using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

/// <summary>
/// Scene-local bundle that describes ONE QTE event.
/// One per QTE in the area scene (e.g. "Park Gate QTE").
///
/// What it holds (all inspector-wired, same scene):
///   • QTEDefinitionSO  — timing/key/ID data
///   • QTETriggerPoint[] — the 2 spots players stand on
///   • CinemachineCamera — QTE-specific camera
///   • MonoBehaviour[]  — activatables fired on success (gates, doors, etc.)
///   • QTEZoneTrigger   — the approach trigger (calls BeginQTE when players enter)
///   • World UI refs    — rootPanel/fillBar/timerRing/labels from the scene's World UI canvas
///
/// How to call a QTE:
///   • QTEZoneTrigger calls anchor.BeginQTE() when both players walk in.
///   • TutorialQTEStepSO calls ctx.qteAnchor.BeginQTE() in the tutorial path.
///   Both delegate to QTEManager.Instance.BeginQTE(this).
///
/// WHY UI IS HERE NOT ON QTEManager:
///   QTEManager is Persistent; each QTE uses a World UI canvas in its area scene.
///   Cross-scene refs don't serialize. The anchor is scene-local so it safely
///   holds refs to the World UI, and passes them to QTEManager at BeginQTE time.
/// </summary>
public class QTESceneAnchor : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private QTEDefinitionSO definition;

    [Header("Scene refs — same scene as this anchor")]
    [SerializeField] private QTETriggerPoint[] triggerPoints;
    [SerializeField] private CinemachineCamera qteCamera;
    [SerializeField] private MonoBehaviour[] activatableMono;

    [Tooltip("The zone trigger that starts this QTE. " +
             "QTEManager calls ResetTrigger() on it when the QTE fails.")]
    [SerializeField] private QTEZoneTrigger zoneTrigger;

    [Header("World UI — wire to elements inside this scene's QTE canvas")]
    [Tooltip("The root panel to show/hide during the mash phase.")]
    [SerializeField] private GameObject rootPanel;
    [Tooltip("Horizontal fill bar showing mash progress (0→1).")]
    [SerializeField] private Image fillBar;
    [Tooltip("Radial fill image for the countdown timer.")]
    [SerializeField] private Image timerRing;
    [SerializeField] private TMP_Text instructionLabel;
    [SerializeField] private TMP_Text countdownLabel;

    // ── Resolved in Awake ──────────────────────────────────────────────────
    private IActivatable[] _activatables;

    public QTEDefinitionSO Definition     => definition;
    public QTETriggerPoint[] TriggerPoints => triggerPoints;
    public CinemachineCamera QTECamera     => qteCamera;
    public QTEZoneTrigger ZoneTrigger      => zoneTrigger;

    // UI accessors — QTEManager pulls these at BeginQTE time
    public GameObject    RootPanel       => rootPanel;
    public Image         FillBar         => fillBar;
    public Image         TimerRing       => timerRing;
    public TMP_Text      InstructionLabel => instructionLabel;
    public TMP_Text      CountdownLabel  => countdownLabel;

    private void Awake()
    {
        _activatables = new IActivatable[activatableMono?.Length ?? 0];
        for (int i = 0; i < _activatables.Length; i++)
            _activatables[i] = activatableMono[i] as IActivatable;
    }

    // ── API ────────────────────────────────────────────────────────────────

    /// <summary>Entry point for QTEZoneTrigger and TutorialQTEStepSO.</summary>
    public void BeginQTE()
    {
        if (QTEManager.Instance == null)
        {
            Debug.LogError("[QTESceneAnchor] QTEManager not found. " +
                           "Is it in Persistent.unity?", this);
            return;
        }
        QTEManager.Instance.BeginQTE(this);
    }

    /// <summary>Called by QTEManager when the mash phase succeeds.</summary>
    public void FireSuccess()
    {
        foreach (var a in _activatables)
            a?.Activate();
    }

    /// <summary>Called by QTEManager to arm/disarm the trigger points.</summary>
    public void SetTriggerPointsActive(bool active)
    {
        foreach (var tp in triggerPoints)
            tp?.SetActive(active);
    }
}
