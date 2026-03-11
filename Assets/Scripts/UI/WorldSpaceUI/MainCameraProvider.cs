using UnityEngine;

public class MainCameraProvider : MonoBehaviour
{
    private Camera mainCameraTransform;
    private void Start()  // was Awake
    {
        if (Camera.main != null)
            mainCameraTransform = Camera.main;
    }
    public Camera GetTargetTransform()
    {
        return mainCameraTransform;
    }
}
