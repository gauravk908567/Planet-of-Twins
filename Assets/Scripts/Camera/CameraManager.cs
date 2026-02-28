using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour, ICameraController
{
    public CinemachineCamera CinemachineTopDownCam { get => cinemachineTopDownCam; set => cinemachineTopDownCam = value; }
    public CinemachineCamera CinemachineCloseCam { get => cinemachineCloseCam; set => cinemachineCloseCam = value; }

    [SerializeField] private CinemachineCamera[] cinemachineCameras;
    [SerializeField] private CinemachineCamera cinemachineCloseCam;
    [SerializeField] private CinemachineCamera cinemachineTopDownCam;

    [SerializeField] private CinemachineCamera startCam;
    private CinemachineCamera currentCam;


    private void Start()
    {
        currentCam = startCam;
        UpdatePriorities();
    }

    private void UpdatePriorities()
    {
        for (int i = 0; i < cinemachineCameras.Length; i++)
        {
            if (cinemachineCameras[i] == currentCam)
            {
                cinemachineCameras[i].Priority = 20;
            }
            else
            {
                cinemachineCameras[i].Priority = 18;
            }
            // Debug.Log("Current active cam " + currentCam.name);
        }
    }

    public void SwitchToCamera(CinemachineCamera targetCamera)
    {
        if (currentCam == targetCamera) return; // Don't switch if already active

        currentCam = targetCamera;
        UpdatePriorities();
    }
}
