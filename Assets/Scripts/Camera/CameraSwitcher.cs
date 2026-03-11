using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private float cameraSwitchToTopDownThreshold = 12f;

    [SerializeField] private GameObject overviewControllerObject;

    private IDistanceProvider distanceProvider;
    private ICameraController cameraController;

    private CameraManager cameraManager;

    private IOverviewBroadcaster overviewBroadcaster;

    private bool isOverviewForced = false;

    private void Awake()
    {
        distanceProvider = GetComponent<IDistanceProvider>();
        cameraManager = GetComponent<CameraManager>();
        cameraController = GetComponent<ICameraController>();

        if (overviewControllerObject != null)
            overviewBroadcaster = overviewControllerObject.GetComponent<IOverviewBroadcaster>();
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
        isOverviewForced = isActive;
        // Instantly force the TopDown camera when the key is pressed
        if (isOverviewForced)
        {
            cameraController.SwitchToCamera(cameraManager.CinemachineOverviewCam);
        }
    }
    private void Update()
    {
        if (isOverviewForced) return;

        float currentDitance = distanceProvider.GetDistance();
        if (currentDitance >= cameraSwitchToTopDownThreshold)
        {
            cameraController.SwitchToCamera(cameraManager.CinemachineTopDownCam);
        }
        else
        {
            cameraController.SwitchToCamera(cameraManager.CinemachineCloseCam);
        }
    }
}
