using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class WorldSpaceCanvasCamera : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Canvas>().worldCamera = Camera.main;
    }
}
