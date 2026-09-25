using UnityEngine;

/// <summary>
/// A single weighted factor in a utility score calculation.
///
/// Designer picks a CurveShape and 1-2 parameters.
/// AnimationCurve is generated automatically — no manual handle dragging.
///
/// X axis = normalised blackboard value (0 to 1)
/// Y axis = score contribution (0 to 1, multiplied by weight)
///
/// Use [UtilityFactorKey] dropdown to pick the blackboard key — no typing.
/// </summary>
[System.Serializable]
public class UtilityFactor
{
    [Tooltip("Blackboard key to read. Pick from dropdown — no manual typing.")]
    [UtilityFactorKey]
    public string blackboardKey = "";

    [Tooltip("Mathematical shape of the response curve. Auto-generates the AnimationCurve.")]
    public CurveShape curveShape = CurveShape.Linear;

    [Tooltip("Controls how steep/aggressive the curve is. Higher = more extreme.\n" +
             "Ignored by Linear, InvertedLinear, Step.")]
    [Range(0.5f, 8f)]
    public float steepness = 2f;

    [Tooltip("Step curve only — X value where curve jumps from 0 to 1.")]
    [Range(0f, 1f)]
    public float stepThreshold = 0.5f;

    [Tooltip("How much this factor contributes to the total score. Higher = more influence.")]
    [Range(0f, 100f)]
    public float weight = 10f;

    [Tooltip("Invert input — high value becomes low. Useful for 'low HP = high urgency'.")]
    public bool invert = false;

    [Tooltip("Treat blackboard value as bool (0 or 1). Curve is bypassed — returns weight directly if true.")]
    public bool isBool = false;

    /// <summary>
    /// Evaluate this factor for a given normalised input value.
    /// Returns a score contribution between 0 and weight.
    /// </summary>
    public float Evaluate(float rawValue)
    {
        if (invert) rawValue = 1f - rawValue;
        rawValue = Mathf.Clamp01(rawValue);

        if (isBool)
            return rawValue >= 0.5f ? weight : 0f;

        float curveValue = CurveGenerator.Evaluate(curveShape, rawValue, steepness, stepThreshold);
        return curveValue * weight;
    }

    /// <summary>
    /// Generate the AnimationCurve for this factor — used by editor preview.
    /// </summary>
    public AnimationCurve GenerateCurve()
        => CurveGenerator.Generate(curveShape, steepness, stepThreshold);
}