using UnityEngine;

public class UIBillboard : MonoBehaviour
{
    private Transform cameraTransform;

    private void Start()
    {
        // Get a reference to the main camera's transform
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("Main Camera not found! Please tag a camera as 'MainCamera'.");
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform != null)
        {
            // Only rotate around the Y-axis to keep the object upright
            transform.rotation = Quaternion.Euler(0, cameraTransform.rotation.eulerAngles.y, 0);
        }
    }
}