using UnityEngine;

/// <summary>
/// Per-player movement dispatch (couch M1.4). Each twin is driven by its OWNING player's input provider
/// (<see cref="PlayerInputRouter.For"/>); twins are resolved from <see cref="PlayerRoster"/>. The soul
/// (rescue/emergency, reworked in M3) still reads shared input.
///
/// <para>The selection-based mirror is GONE — both twins move normally (MirroredMovementModifier deleted;
/// a null movement modifier is normal movement). With P2's device unwired, <c>For(TwinB)</c> falls back to
/// P1's provider, so both twins move together (single-device intermediate) until the second device is paired.</para>
///
/// Subscribes CameraManager.OnMovementModeChanged: true → camera-relative movement (tutorial/external cam),
/// false → raw world-Z movement (standard gameplay cam).
/// </summary>
public class TwinMovementDispatcher : MonoBehaviour
{
    [SerializeField] private Player soulTwin;

    [Tooltip("Source of OnMovementModeChanged event.")]
    [SerializeField] private CameraManager cameraManager;

    [Tooltip("Unity Camera used for direction reference in camera-relative mode.")]
    [SerializeField] private Camera referenceCamera;

    private bool _useCameraRelative = false;

    private void Awake()
    {
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
        var roster = PlayerRoster.Instance;
        if (roster != null)
        {
            MoveTwin(roster.TwinA);   // P1's provider (slot One)
            MoveTwin(roster.TwinB);   // P2's provider (slot Two; falls back to P1 until wired)
        }

        // Soul only when active — rescue/emergency entity, reworked in M3; reads shared input for now.
        if (soulTwin != null && soulTwin.gameObject.activeSelf)
        {
            var shared = PlayerInputRouter.SharedInput;
            if (shared != null)
                soulTwin.Movement?.ExecuteCommand(BuildCommand(shared.GetMovementInput()));
        }
    }

    private void MoveTwin(Player twin)
    {
        if (twin == null) return;
        var input = PlayerInputRouter.For(twin);
        if (input == null) return;
        // Frozen/locked twin ignores the command internally (PlayerMovementController).
        twin.Movement?.ExecuteCommand(BuildCommand(input.GetMovementInput()));
    }

    private IPlayerCommand BuildCommand(Vector2 raw)
    {
        Vector2 finalInput = _useCameraRelative ? GetCameraRelativeInput(raw) : raw;
        return new MoveCommand(finalInput);
    }

    /// <summary>
    /// Converts raw 2D input into world-space XZ direction relative to the reference camera's current
    /// orientation. W = away from camera, S = toward camera, A/D = camera left/right.
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
