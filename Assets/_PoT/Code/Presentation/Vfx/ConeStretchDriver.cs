using UnityEngine;

/// <summary>
/// PROTOTYPE ONLY (TestLab). Drives the ConeStretch shader's _Height over time so the
/// question-mark cone whips UP from the pivot (dot), overshoots, settles, holds, then loops.
/// Runs on UNSCALED time (ignores Setsuna/pause) via a MaterialPropertyBlock (no material leak).
/// Tune the whole feel in the Inspector: riseTime = whip speed, the curve = overshoot amount,
/// period = how long it holds before repeating. Cone shape/angle live on the material (_ConeAngle).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ConeStretchDriver : MonoBehaviour
{
    [Tooltip("Full loop length (seconds). It whips up, holds, then restarts.")]
    public float period = 3.5f;

    [Tooltip("How long the whip-up takes (seconds).")]
    public float riseTime = 0.35f;

    [Tooltip("_Height over normalized rise time: overshoot then settle to 1. Y peak = cone length.")]
    public AnimationCurve rise = new AnimationCurve(
        new Keyframe(0f, 0.10f), new Keyframe(0.45f, 1.90f), new Keyframe(0.75f, 1.0f), new Keyframe(1f, 1.0f));

    private Renderer _r;
    private MaterialPropertyBlock _mpb;
    private float _t;
    private static readonly int HeightId = Shader.PropertyToID("_Height");

    private void Awake()
    {
        _r = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;     // unscaled: prototype must read the same under Setsuna
        if (_t > period) _t = 0f;

        float riseN = riseTime > 0.0001f ? Mathf.Clamp01(_t / riseTime) : 1f;
        float h = rise.Evaluate(riseN);   // holds at the curve's last value after riseTime

        _r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HeightId, h);
        _r.SetPropertyBlock(_mpb);
    }
}
