using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour, ICameraController
{
    public CinemachineCamera CinemachineTopDownCam { get => cinemachineTopDownCam; set => cinemachineTopDownCam = value; }
    public CinemachineCamera CinemachineCloseCam { get => cinemachineCloseCam; set => cinemachineCloseCam = value; }
    public CinemachineCamera CinemachineOverviewCam { get => cinemachineOverviewCam; set => cinemachineOverviewCam = value; }

    /// <summary>
    /// Fired when the active camera changes.
    /// true  = camera-relative movement should be used (external/tutorial cam active)
    /// false = raw world-Z movement (standard gameplay cam active)
    /// TwinMovementDispatcher subscribes to this.
    /// </summary>
    public event System.Action<bool> OnMovementModeChanged;

    [Tooltip("All cameras that participate in distance-based switching. " +
             "Includes both gameplay AND tutorial transposers. " +
             "QTE cameras do NOT go here — SwitchToCamera handles them directly.")]
    [SerializeField] private CinemachineCamera[] cinemachineCameras;

    [Header("Gameplay cameras")]
    [SerializeField] private CinemachineCamera cinemachineCloseCam;
    [SerializeField] private CinemachineCamera cinemachineTopDownCam;
    [SerializeField] private CinemachineCamera cinemachineOverviewCam;

    [Header("Tutorial cameras — same distance logic as gameplay cams")]
    [SerializeField] private CinemachineCamera tutorialCloseCam;
    [SerializeField] private CinemachineCamera tutorialTopDownCam;

    [SerializeField] private CinemachineCamera startCam;

    // ── Public accessors for tutorial cams ───────────────────────────────
    public CinemachineCamera TutorialCloseCam => tutorialCloseCam;
    public CinemachineCamera TutorialTopDownCam => tutorialTopDownCam;

    private CinemachineCamera _currentCam;
    private CinemachineCamera _previousExternalCam;

    // Priorities — hardcoded, no serialised fields needed
    // QTE/external cams: 30 (always win)
    // Overview cam:      20
    // Gameplay cams:     10
    // Inactive:           0
    private const int QTEPriority = 30;
    private const int OverviewPriority = 20;
    private const int GameplayPriority = 10;

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
    /// Demotes a QTE/external camera back to inactive priority without switching to any
    /// specific gameplay camera. Call this after a QTE ends — CameraSwitcher.Update() will
    /// pick the correct camera (tutorial or gameplay mode) on the very next frame.
    /// </summary>
    public void DemoteExternalCamera()
    {
        if (_previousExternalCam != null)
        {
            _previousExternalCam.Priority = 0;
            _previousExternalCam = null;
        }
        _currentCam = null; // lets the next SwitchToCamera call bypass the early-return guard
    }

    private void UpdatePriorities(CinemachineCamera active)
    {
        // Demote previous external cam
        if (_previousExternalCam != null && _previousExternalCam != active)
        {
            _previousExternalCam.Priority = 0;
            _previousExternalCam = null;
        }

        bool isInManagedArray = false;

        for (int i = 0; i < cinemachineCameras.Length; i++)
        {
            if (cinemachineCameras[i] == null) continue;

            if (cinemachineCameras[i] == active)
            {
                cinemachineCameras[i].Priority =
                    cinemachineCameras[i] == cinemachineOverviewCam
                    ? OverviewPriority
                    : GameplayPriority;
                isInManagedArray = true;
            }
            else
            {
                cinemachineCameras[i].Priority = 0;
            }
        }

        // External cam (QTE, cutscene) — always wins
        if (!isInManagedArray)
        {
            active.Priority = QTEPriority;
            _previousExternalCam = active;
        }

        // ── Broadcast movement mode ───────────────────────────────────────
        // Camera-relative movement is needed when an external cam is active
        // OR when the active cam is one of the tutorial cams.
        bool isTutorialCam = active == tutorialCloseCam || active == tutorialTopDownCam;
        bool needsCameraRelative = !isInManagedArray || isTutorialCam;
        OnMovementModeChanged?.Invoke(needsCameraRelative);
    }
}