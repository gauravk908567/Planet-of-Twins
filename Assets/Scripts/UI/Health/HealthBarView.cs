using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The health-bar view every health consumer talks to. Deliberately a CONCRETE type on
/// <see cref="IndividualHealthPresenter"/>, <see cref="IndividualHealthUI"/> and
/// <see cref="WorldSpaceHealthUI"/>: with the field typed concretely the Inspector refuses
/// anything that is not a health-bar view. That guarantee is the reason those three fields are
/// NOT widened to <see cref="IHealthBarView"/> (game.md 17.2b, row 3).
///
/// Two mutually exclusive render paths, chosen by whether <c>barView</c> is assigned:
///   • <c>barView</c> EMPTY  — legacy Slider + tint. Byte-identical to the original behaviour.
///   • <c>barView</c> SET    — the authored PoT/UIBar art. The Slider is bypassed entirely and
///                             <see cref="UIBarView"/> owns fill smoothing and the low flash.
/// The art swap is therefore purely additive: no consumer signature changed, and clearing one
/// serialized slot restores the old bar.
/// </summary>
public class HealthBarView : MonoBehaviour, IHealthBarView
{
    [Header("New art path — assign to use the authored PoT/UIBar bar")]
    [Tooltip("Optional. When assigned, this drives the bar and the Slider/tint fields below are " +
             "ignored. Leave empty to keep the original Slider behaviour.")]
    [SerializeField] private UIBarView barView;

    [Header("Legacy Slider path (used only while Bar View is empty)")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("The fill Image on the Slider. Assign the Fill Area > Fill child.")]
    [SerializeField] private Image fillImage;

    [SerializeField] private Color normalColour = Color.green;
    [SerializeField] private Color criticalColour = Color.red;

    [Tooltip("Fill fraction below which the critical colour activates.")]
    [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.20f;

    // ── IHealthBarView ────────────────────────────────────────────────────────────────
    public void SetFill(float normalised)
    {
        if (barView != null)
        {
            // UIBarView smooths toward the target itself and derives its own low-health flash
            // from the displayed value, so no critical-state call is needed on this path.
            barView.SetValue(normalised);
            return;
        }

        if (healthSlider != null)
            healthSlider.value = Mathf.Clamp01(normalised);

        // Auto-detect critical from fill value
        SetCriticalState(normalised <= criticalThreshold);
    }

    public void SetCriticalState(bool isCritical)
    {
        // On the art path the flash is a continuous function of the displayed value inside
        // UIBarView — overriding a colour here would fight it, so this is a deliberate no-op.
        if (barView != null) return;

        if (fillImage != null)
            fillImage.color = isCritical ? criticalColour : normalColour;
    }
}