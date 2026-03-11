using System;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarView : MonoBehaviour, IHealthBarView
{
    [SerializeField] private Slider healthSlider;

    [Tooltip("The fill Image on the Slider. Assign the Fill Area > Fill child.")]
    [SerializeField] private Image fillImage;

    [SerializeField] private Color normalColour = Color.green;
    [SerializeField] private Color criticalColour = Color.red;

    [Tooltip("Fill fraction below which the critical colour activates.")]
    [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.20f;

    // ?? IHealthBarView ?????????????????????????????????????????
    public void SetFill(float normalised)
    {
        if (healthSlider != null)
            healthSlider.value = Mathf.Clamp01(normalised);

        // Auto-detect critical from fill value
        SetCriticalState(normalised <= criticalThreshold);
    }

    public void SetCriticalState(bool isCritical)
    {
        if (fillImage != null)
            fillImage.color = isCritical ? criticalColour : normalColour;
    }
}