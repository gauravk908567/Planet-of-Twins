using UnityEngine;

/// <summary>
/// Makes a world-space UI element face the camera.
/// Designer controls which axes are affected via Inspector toggles.
/// Default: Y only (upright billboard — most common for world-space health bars).
/// </summary>
public class UIBillboard : MonoBehaviour
{
    [Header("Axis Lock — which axes rotate to face camera")]
    [Tooltip("Rotate X axis to face camera (tilts panel up/down toward camera)")]
    [SerializeField] private bool _lockX = false;
    [Tooltip("Rotate Y axis to face camera (turns panel left/right toward camera)")]
    [SerializeField] private bool _lockY = true;
    [Tooltip("Rotate Z axis to face camera (rolls panel toward camera)")]
    [SerializeField] private bool _lockZ = false;

    private Transform _cameraTransform;

    private void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;
        else
            Debug.LogError("[UIBillboard] Main Camera not found. Tag a camera as MainCamera.", this);
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null) return;

        Vector3 camEuler = _cameraTransform.rotation.eulerAngles;

        transform.rotation = Quaternion.Euler(
            _lockX ? camEuler.x : transform.rotation.eulerAngles.x,
            _lockY ? camEuler.y : transform.rotation.eulerAngles.y,
            _lockZ ? camEuler.z : transform.rotation.eulerAngles.z
        );
    }
}