using UnityEngine;

public class MainCameraProvider : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Awake()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    public Transform GetTargetTransform()
    {
        return mainCameraTransform;
    }
}
