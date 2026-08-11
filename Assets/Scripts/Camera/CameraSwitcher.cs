using UnityEngine;

/// <summary>
/// Drives distance-based camera switching for both gameplay and tutorial cameras.
///
/// Gameplay mode:  switches between CinemachineCloseCam / CinemachineTopDownCam
/// Tutorial mode:  switches between TutorialCloseCam   / TutorialTopDownCam
///
/// Mode is set externally via SetTutorialMode(bool) — call this from a
/// Timeline Signal when the tutorial starts and again when it ends.
///
/// QTE suppression still works identically — SuppressAutoSwitch(true) hands
/// camera control to QTEController entirely.
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private float cameraSwitchToTopDownThreshold = 12f;
    [SerializeField] private GameObject overviewControllerObject;

    private IDistanceProvider distanceProvider;
    private ICameraController cameraController;
    private CameraManager cameraManager;
    private IOverviewBroadcaster overviewBroadcaster;

    private bool _isOverviewForced = false;
    private bool _isQTESuppressed = false;
    private bool _isTutorialMode = false;

    [SerializeField] private bool startInTutorialMode = false;
    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Call with true when QTE starts, false when it ends.
    /// Blocks distance-based and overview switching while active.
    /// </summary>
    private void Start()
    {
        _isTutorialMode = startInTutorialMode;
    }

    public void SuppressAutoSwitch(bool suppress)
    {
        _isQTESuppressed = suppress;

        if (!suppress && _isOverviewForced)
            _isOverviewForced = false;
    }

    /// <summary>
    /// Call from Timeline Signal at tutorial start (true) and tutorial end (false).
    /// Switches the distance-based pair to tutorial cams or gameplay cams.
    /// </summary>
    public void SetTutorialMode(bool tutorial)
    {
        _isTutorialMode = tutorial;
    }

    // ── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        distanceProvider = GetComponent<IDistanceProvider>();
        cameraManager = GetComponent<CameraManager>();
        cameraController = GetComponent<ICameraController>();

        if (overviewControllerObject != null)
            overviewBroadcaster =
                overviewControllerObject.GetComponent<IOverviewBroadcaster>();
    }

    private void OnEnable()
    {
        if (overviewBroadcaster != null)
            overviewBroadcaster.OnOverviewToggled += HandleOverview;
    }

    private void OnDisable()
    {
        if (overviewBroadcaster != null)
            overviewBroadcaster.OnOverviewToggled -= HandleOverview;
    }

    private void HandleOverview(bool isActive)
    {
        if (_isQTESuppressed) return;
        _isOverviewForced = isActive;
        if (_isOverviewForced)
            cameraController.SwitchToCamera(cameraManager.CinemachineOverviewCam);
    }

    private void Update()
    {
        if (_isQTESuppressed) return;
        if (_isOverviewForced) return;

        float dist = distanceProvider.GetDistance();

        if (_isTutorialMode)
        {
            // Tutorial distance-based switching
            if (dist >= cameraSwitchToTopDownThreshold)
                cameraController.SwitchToCamera(cameraManager.TutorialTopDownCam);
            else
                cameraController.SwitchToCamera(cameraManager.TutorialCloseCam);
        }
        else
        {
            // Gameplay distance-based switching
            if (dist >= cameraSwitchToTopDownThreshold)
                cameraController.SwitchToCamera(cameraManager.CinemachineTopDownCam);
            else
                cameraController.SwitchToCamera(cameraManager.CinemachineCloseCam);
        }
    }
}