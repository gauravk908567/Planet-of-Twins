using UnityEngine;

/// <summary>
/// Defines the mathematical shape of a utility response curve.
/// Designer picks a shape + 1-2 parameters — curve generates automatically.
/// No manual AnimationCurve handle dragging needed.
///
/// X axis = normalised input value (0 to 1)
/// Y axis = score contribution (0 to 1)
/// </summary>
public enum CurveShape
{
    /// <summary>Straight line. Y rises proportionally with X. Good for simple proportional responses.</summary>
    Linear,

    /// <summary>Slow start, fast finish. Exponential rise. Good for "urgency builds as value rises" — e.g. attack eagerness as faction energy rises.</summary>
    EaseIn,

    /// <summary>Fast start, slow finish. Diminishing returns. Good for "first gains matter most" — e.g. confidence from having allies.</summary>
    EaseOut,

    /// <summary>Slow-fast-slow. Good for threshold responses with a clear peak zone — e.g. optimal attack range.</summary>
    SCurve,

    /// <summary>Flat at 0, instant jump to 1 at threshold. Good for binary states — e.g. HasTarget, IsRescueActive.</summary>
    Step,

    /// <summary>High at 0, drops to 0 as X rises. Inverse linear. Good for "penalty as value rises" — e.g. caution as own HP drops.</summary>
    InvertedLinear,

    /// <summary>High at 0, exponential drop. Good for "strong aversion that eases" — e.g. fleeing urgency as distance increases.</summary>
    InvertedEaseIn,
}

/// <summary>
/// Generates AnimationCurves mathematically from CurveShape + parameters.
/// Eliminates manual curve drawing — curves are exact and reproducible.
/// </summary>
public static class CurveGenerator
{
    private const int SampleCount = 20;

    /// <summary>
    /// Generate an AnimationCurve from a shape and parameters.
    /// steepness: controls how aggressive the curve is (default 2.0).
    /// threshold: used by Step to set the jump point (default 0.5).
    /// </summary>
    public static AnimationCurve Generate(CurveShape shape,
                                          float steepness = 2f,
                                          float threshold = 0.5f)
    {
        var keys = new Keyframe[SampleCount];
        steepness = Mathf.Max(0.1f, steepness);
        threshold = Mathf.Clamp01(threshold);

        for (int i = 0; i < SampleCount; i++)
        {
            float x = i / (float)(SampleCount - 1);
            float y = Evaluate(shape, x, steepness, threshold);
            keys[i] = new Keyframe(x, y);
        }

        var curve = new AnimationCurve(keys);
        // Smooth tangents so curve looks clean in Inspector
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0.33f);

        return curve;
    }

    public static float Evaluate(CurveShape shape, float x,
                                 float steepness = 2f, float threshold = 0.5f)
    {
        x = Mathf.Clamp01(x);
        switch (shape)
        {
            case CurveShape.Linear:
                return x;

            case CurveShape.EaseIn:
                return Mathf.Pow(x, steepness);

            case CurveShape.EaseOut:
                return 1f - Mathf.Pow(1f - x, steepness);

            case CurveShape.SCurve:
                // Logistic sigmoid centred at 0.5
                float k = steepness * 8f;
                float sig = 1f / (1f + Mathf.Exp(-k * (x - 0.5f)));
                // Normalise so endpoints are exactly 0 and 1
                float sig0 = 1f / (1f + Mathf.Exp(k * 0.5f));
                float sig1 = 1f / (1f + Mathf.Exp(-k * 0.5f));
                return (sig - sig0) / (sig1 - sig0);

            case CurveShape.Step:
                return x >= threshold ? 1f : 0f;

            case CurveShape.InvertedLinear:
                return 1f - x;

            case CurveShape.InvertedEaseIn:
                return 1f - Mathf.Pow(x, steepness);

            default:
                return x;
        }
    }
}