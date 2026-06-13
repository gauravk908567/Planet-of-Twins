using UnityEngine;

/// <summary>
/// Surrounds a single tutorial zone with a trigger collider.
/// If either twin exits, FailureResetSequencer fires and twins
/// are placed back at the reset position Transforms.
///
/// activeStage — only fires during this stage. None = always active.
///
/// Exposes ActiveStage, LeftResetPosition, RightResetPosition as public
/// properties so TutorialOuterBoundary can read them without duplicating logic.
///
/// SETUP:
///   Add to a GO with a Box Collider (Is Trigger ON).
///   Size the collider to cover the allowed zone area.
///   Create two empty child GOs: LeftResetPoint / RightResetPoint.
///   Place them where twins should reappear after a boundary violation.
///   Wire those Transforms to leftResetPoint and rightResetPoint.
///   Set activeStage to match the tutorial stage this zone covers.
/// </summary>
public class TutorialBoundary : MonoBehaviour
{
    [Tooltip("Empty GO placed at Lyra's reset position in scene.")]
    [SerializeField] private Transform leftResetPoint;

    [Tooltip("Empty GO placed at Kai's reset position in scene.")]
    [SerializeField] private Transform rightResetPoint;

    [Header("Message shown on boundary exit")]
    [SerializeField] private string boundaryMessage = "Stay in the area";

    [Header("Only active during this stage — None = always active")]
    [SerializeField] private TutorialStage activeStage = TutorialStage.None;

    [Header("Zone highlight particles — child of this GO")]
    [SerializeField] private ParticleSystem[] boundaryGlowParticles;

    // ── Public properties — read by TutorialOuterBoundary ─────────
    public TutorialStage ActiveStage => activeStage;

    public Vector3 LeftResetPosition => leftResetPoint != null
        ? leftResetPoint.position : transform.position;

    public Vector3 RightResetPosition => rightResetPoint != null
        ? rightResetPoint.position : transform.position;

    // ── Runtime resolved (Persistent singletons — R4) ────────────
    private Player _leftTwin;
    private Player _rightTwin;
    private FailureResetSequencer _resetSequencer;
    private FailureNotice _failureNotice;

    private void Start()
    {
        _resetSequencer = FailureResetSequencer.Instance;
        _failureNotice = FailureNotice.Instance;
        var selector = TwinSelector.Instance;
        if (selector != null)
        {
            _leftTwin = selector.LeftTwin;
            _rightTwin = selector.RightTwin;
        }
        if (_resetSequencer == null)
            Debug.LogError("[TutorialBoundary] FailureResetSequencer.Instance is null — is Persistent loaded?", this);
        if (_leftTwin == null || _rightTwin == null)
            Debug.LogError("[TutorialBoundary] Twins unresolved via TwinSelector.Instance — is Persistent loaded?", this);
    }

    // ── Internal ──────────────────────────────────────────────────
    private bool _resetting = false;
    private void OnEnable()
    {
        _resetting = false;
        if (TutorialContext.Instance != null)
            TutorialContext.Instance.OnStageChanged += OnStageChanged;
        RefreshGlow();
    }

    private void OnDisable()
    {
        _resetting = false;
        if (TutorialContext.Instance != null)
            TutorialContext.Instance.OnStageChanged -= OnStageChanged;
        SetGlowActive(false);
    }


    private void OnTriggerExit(Collider other)
    {
        if (_resetting) return;

        if (activeStage != TutorialStage.None &&
            TutorialContext.Instance?.CurrentStage != activeStage) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;
        if (player != _leftTwin && player != _rightTwin) return;

        if (leftResetPoint == null || rightResetPoint == null)
        {
            Debug.LogWarning("[TutorialBoundary] Reset points not assigned.", this);
            return;
        }

        TriggerReset();
    }

    /// <summary>
    /// Public — called by TutorialOuterBoundary when it wants this zone
    /// to handle the reset. Fires the sequencer with this zone's reset points.
    /// Safe to call while already resetting — guarded internally.
    /// </summary>
    public void TriggerReset()
    {
        if (_resetting) return;
        _resetting = true;
        _failureNotice?.Show(boundaryMessage);
        _resetSequencer?.TriggerReset(
            LeftResetPosition,
            RightResetPosition,
            () => _resetting = false);
    }


    private void OnStageChanged(TutorialStage stage)
    {
        RefreshGlow();
    }

    private void RefreshGlow()
    {
        bool shouldGlow = activeStage != TutorialStage.None &&
                          TutorialContext.Instance?.CurrentStage == activeStage;
        SetGlowActive(shouldGlow);
    }

    private void SetGlowActive(bool active)
    {
        if (boundaryGlowParticles == null) return;
        foreach (var ps in boundaryGlowParticles)
        {
            if (ps == null) continue;
            if (active)
            {
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}