using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour, ICameraController
{
    public CinemachineCamera CinemachineTopDownCam { get => cinemachineTopDownCam; set => cinemachineTopDownCam = value; }
    public CinemachineCamera CinemachineCloseCam { get => cinemachineCloseCam; set => cinemachineCloseCam = value; }
    public CinemachineCamera CinemachineOverviewCam { get => cinemachineOverviewCam; set => cinemachineOverviewCam = value; }

    [Tooltip("All cameras that participate in distance-based switching. " +
             "QTE cameras do NOT need to be here — SwitchToCamera handles them directly.")]
    [SerializeField] private CinemachineCamera[] cinemachineCameras;
    [SerializeField] private CinemachineCamera cinemachineCloseCam;
    [SerializeField] private CinemachineCamera cinemachineTopDownCam;
    [SerializeField] private CinemachineCamera cinemachineOverviewCam;
    [SerializeField] private CinemachineCamera startCam;

    private CinemachineCamera _currentCam;

    private void Start()
    {
        _currentCam = startCam;
        UpdatePriorities(_currentCam);
    }

    public void SwitchToCamera(CinemachineCamera targetCamera)
    {
        if (targetCamera == null || _currentCam == targetCamera) return;
        _currentCam = targetCamera;
        UpdatePriorities(_currentCam);
    }

    /// <summary>
    /// FIX: old version only iterated cinemachineCameras[], so any camera
    /// outside that array (QTE cams, cutscene cams) never got priority 20
    /// and therefore never became active.
    ///
    /// New version: set priority 20 on the target directly, then demote
    /// everything in the managed array that isn't the target.
    /// Cameras outside the array that were previously elevated are demoted
    /// via the _previousExternalCam reference so they don't stay active
    /// after their QTE ends.
    /// </summary>
    private CinemachineCamera _previousExternalCam;

    private void UpdatePriorities(CinemachineCamera active)
    {
        // Demote previous external camera (e.g. QTE cam returning to gameplay)
        if (_previousExternalCam != null && _previousExternalCam != active)
        {
            _previousExternalCam.Priority = 0;
            _previousExternalCam = null;
        }

        bool isInManagedArray = false;

        // Update managed cameras
        for (int i = 0; i < cinemachineCameras.Length; i++)
        {
            if (cinemachineCameras[i] == null) continue;

            if (cinemachineCameras[i] == active)
            {
                cinemachineCameras[i].Priority = 20;
                isInManagedArray = true;
            }
            else
            {
                cinemachineCameras[i].Priority = 0;
            }
        }

        // Camera is not in the managed array (QTE cam, cutscene cam, etc.)
        // Give it the highest priority directly and track it for later demotion.
        if (!isInManagedArray)
        {
            active.Priority = 20;
            _previousExternalCam = active;
        }
    }
}