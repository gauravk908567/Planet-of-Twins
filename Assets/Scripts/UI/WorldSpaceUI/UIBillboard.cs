using UnityEngine;

public class UIBillboard : MonoBehaviour
{
    private ILookTargetProvider targetProvider;
    private Transform targetTransform;

    private void Awake()
    {
        targetProvider = GetComponent<ILookTargetProvider>();
    }

    private void OnEnable()
    {
        if (targetProvider != null)
        {
            targetTransform = targetProvider.GetTargetTransform();
        }
    }

    private void LateUpdate()
    {
        if (targetTransform != null)
        {
            transform.LookAt(transform.position + targetTransform.rotation * Vector3.forward,
                             targetTransform.rotation * Vector3.up);
        }
    }
}
