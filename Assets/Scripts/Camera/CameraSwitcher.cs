using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private float cameraSwitchToTopDownThreshold = 12f;

    private IDistanceProvider distanceProvider;
    private ICameraController cameraController;

    private CameraManager cameraManager;

    private void Awake()
    {
        distanceProvider = GetComponent<IDistanceProvider>();
        cameraManager = GetComponent<CameraManager>();
        cameraController = GetComponent<ICameraController>();
    }

    private void Update()
    {
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
