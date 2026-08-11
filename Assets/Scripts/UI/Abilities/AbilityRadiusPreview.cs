using UnityEngine;

public class AbilityRadiusPreview : MonoBehaviour
{
    [SerializeField] private GameObject previewObject;

    private void Awake()
    {
        previewObject.SetActive(false);
        // Retired visual (user call 2026-07-10, same treatment as the teleport disc): the range read
        // is the scaled ability cue itself (OnStun_Active / Possess_Active ground circle via
        // CueContext.scale). API and radius math stay for any logic callers; nothing renders.
        foreach (var r in previewObject.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
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