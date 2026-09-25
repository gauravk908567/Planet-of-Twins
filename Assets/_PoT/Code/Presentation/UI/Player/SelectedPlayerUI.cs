using UnityEngine;

/// <summary>
/// COUCH D3 — inert. There is no "selected" twin in co-op (each player permanently drives their own),
/// so the old selected/unselected body-material swap is meaningless. This now just applies the plain
/// (unselected) material once at Start so twins render normally, with NO <c>TwinSelector</c> /
/// <c>OnTwinSelected</c> coupling. Kai/Lyra are already visually distinct and character-select tells each
/// player who's who, so there's no identity marker to build. Scheduled for removal in the post-M5 cleanup.
/// </summary>
public class SelectedPlayerUI : MonoBehaviour
{
    [SerializeField] private Material unselectedMaterial;   // the plain material applied to twins
    [SerializeField] private Renderer[] characterRenderers;

    private void Start()
    {
        ApplyMaterial(unselectedMaterial);
    }

    private void ApplyMaterial(Material matToApply)
    {
        if (matToApply == null || characterRenderers == null) return;

        foreach (Renderer rend in characterRenderers)
        {
            if (rend != null)
                rend.sharedMaterial = matToApply;
        }
    }
}
