using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private float cameraSwitchToTopDownThreshold = 12f;
    [SerializeField] private GameObject overviewControllerObject;

    private IDistanceProvider distanceProvider;
    private ICameraController cameraController;
    private CameraManager cameraManager;
    private IOverviewBroadcaster overviewBroadcaster;

    private bool _isOverviewForced = false;
    private bool _isQTESuppressed = false;  // true while a QTE camera is active

    // ── Public API — called by QTEController ──────────────────
    /// <summary>
    /// Call with true when a QTE starts, false when it ends.
    /// Blocks both distance-based switching AND overview cam while active.
    /// </summary>
    public void SuppressAutoSwitch(bool suppress)
    {
        _isQTESuppressed = suppress;

        // If QTE just ended and overview was held during it, force-end the overview
        // so we don't snap to bird's-eye the moment QTE finishes.
        if (!suppress && _isOverviewForced)
        {
            _isOverviewForced = false;
            // CameraSwitcher.Update will resume normal distance-based logic next frame
        }
    }

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
        // FIX: ignore overview input while a QTE is running
        if (_isQTESuppressed) return;

        _isOverviewForced = isActive;
        if (_isOverviewForced)
            cameraController.SwitchToCamera(cameraManager.CinemachineOverviewCam);
    }

    private void Update()
    {
        // QTE active — hands-off entirely, QTEController owns the camera
        if (_isQTESuppressed) return;

        // Overview forced — hands-off, OverviewCamController owns the camera
        if (_isOverviewForced) return;

        // Normal distance-based switching
        float dist = distanceProvider.GetDistance();
        if (dist >= cameraSwitchToTopDownThreshold)
            cameraController.SwitchToCamera(cameraManager.CinemachineTopDownCam);
        else
            cameraController.SwitchToCamera(cameraManager.CinemachineCloseCam);
    }
}