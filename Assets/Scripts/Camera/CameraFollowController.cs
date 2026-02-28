using UnityEngine;
using Unity.Cinemachine;

public class CameraFollowController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private void OnEnable()
    {
       // TwinManager.OnTwinSelected += SetTarget;
    }

    private void OnDisable()
    {
       // TwinManager.OnTwinSelected -= SetTarget;
    }

    private void SetTarget(Transform target)
    {
        cinemachineCamera.Follow = target;
        cinemachineCamera.LookAt = target;
    }
}