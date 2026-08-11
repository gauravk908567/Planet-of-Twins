using UnityEngine;

/// <summary>
/// Dispatches movement input to both twins and the soul.
///
/// Subscribes to CameraManager.OnMovementModeChanged.
/// When true  → camera-relative movement (tutorial/external cam active)
/// When false → raw world-Z movement (standard gameplay cam active)
///
/// SETUP:
///   Wire cameraManager in Inspector.
///   Wire referenceCamera → your main Unity Camera GO.
///   No tag setup needed — CameraManager owns the mode decision.
/// </summary>
public class TwinMovementDispatcher : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private Player soulTwin;
    [SerializeField] private MonoBehaviour inputProviderObject;

    [Tooltip("Source of OnMovementModeChanged event.")]
    [SerializeField] private CameraManager cameraManager;

    [Tooltip("Unity Camera used for direction reference in camera-relative mode.")]
    [SerializeField] private Camera referenceCamera;

    private IInputProvider _input;
    private bool _useCameraRelative = false;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        if (_input == null)
            Debug.LogError("[TwinMovementDispatcher] inputProviderObject missing IInputProvider.", this);

        if (referenceCamera == null)
            referenceCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (cameraManager != null)
            cameraManager.OnMovementModeChanged += SetMovementMode;
    }

    private void OnDisable()
    {
        if (cameraManager != null)
            cameraManager.OnMovementModeChanged -= SetMovementMode;
    }

    private void SetMovementMode(bool cameraRelative)
        => _useCameraRelative = cameraRelative;

    private void Update()
    {
        if (_input == null) return;

        Vector2 raw = _input.GetMovementInput();
        Vector2 finalInput = _useCameraRelative
            ? GetCameraRelativeInput(raw)
            : raw;

        IPlayerCommand cmd = new MoveCommand(finalInput);

        // Both twins always receive input — frozen twin ignores it internally
        leftTwin?.Movement?.ExecuteCommand(cmd);
        rightTwin?.Movement?.ExecuteCommand(cmd);

        // Soul only when active
        if (soulTwin != null && soulTwin.gameObject.activeSelf)
            soulTwin.Movement?.ExecuteCommand(cmd);
    }

    /// <summary>
    /// Converts raw 2D input into world-space XZ direction relative to
    /// the reference camera's current orientation.
    /// W = away from camera, S = toward camera, A/D = camera left/right.
    /// </summary>
    private Vector2 GetCameraRelativeInput(Vector2 raw)
    {
        if (raw.sqrMagnitude < 0.001f) return Vector2.zero;

        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam == null) return raw;

        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;

        camForward.y = 0f;
        camRight.y = 0f;

        // Guard: camera pointing straight down — fall back to world forward
        if (camForward.sqrMagnitude < 0.001f)
            camForward = Vector3.forward;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * raw.y + camRight * raw.x;
        return new Vector2(moveDir.x, moveDir.z);
    }
}