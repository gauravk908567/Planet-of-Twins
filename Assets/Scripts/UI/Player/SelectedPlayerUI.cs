using UnityEngine;

public class SelectedPlayerUI : MonoBehaviour
{
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material unselectedMaterial;

    [SerializeField] private Renderer[] characterRenderers;

    [SerializeField] private GameObject selectionManagerObject;

    private ISelectionBroadcaster selectionBroadcaster;

    private void Awake()
    {
        if (selectionManagerObject != null)
        {
            selectionBroadcaster = selectionManagerObject.GetComponent<ISelectionBroadcaster>();

            if (selectionBroadcaster == null)
            {
                Debug.LogError("The assigned object does not implement ISelectionBroadcaster!");
            }
        }
    }

    private void OnEnable()
    {
        if (selectionBroadcaster != null)
        {
            selectionBroadcaster.OnTwinSelected += HandleSelectionChanged;
        }
    }

    private void OnDisable()
    {
        if (selectionBroadcaster != null)
        {
            selectionBroadcaster.OnTwinSelected -= HandleSelectionChanged;
        }
    }

    private void HandleSelectionChanged(Transform newlySelectedTwin)
    {
        if (newlySelectedTwin == this.transform)
        {
            ApplyMaterial(selectedMaterial);
        }
        else
        {
            ApplyMaterial(unselectedMaterial);
        }
    }

    private void ApplyMaterial(Material matToApply)
    {
        if (matToApply == null || characterRenderers == null) return;

        foreach (Renderer rend in characterRenderers)
        {
            if (rend != null)
            {
                rend.sharedMaterial = matToApply;
            }
        }
    }
}
