using UnityEngine;

public class AbilityRadiusPreview : MonoBehaviour
{
    [SerializeField] private GameObject previewObject;

    private void Awake()
    {
        previewObject.SetActive(false);
    }

    public void Show(float radius)
    {
        previewObject.SetActive(true);

        float diameter = radius * 2f;

        previewObject.transform.localScale =
            new Vector3(diameter, 0.01f, diameter);
    }

    public void Hide()
    {
        previewObject.SetActive(false);
    }
}