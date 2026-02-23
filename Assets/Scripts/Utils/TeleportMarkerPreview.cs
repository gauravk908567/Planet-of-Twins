using UnityEngine;

public class TeleportMarkerPreview : MonoBehaviour
{
    [SerializeField] private GameObject markerObject;

    private void Awake()
    {
        markerObject.SetActive(false);
    }


    public void Show(Transform otherTwinPos)
    {
        markerObject.SetActive(true);
        markerObject.transform.position = transform.position;
    }

    public void Hide()
    {
        markerObject.SetActive(false);
    }
}