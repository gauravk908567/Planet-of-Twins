using UnityEngine;

/// <summary>
/// Rigid pendulum sway for HANGING WHOLE objects (lanterns, signs, wind chimes):
/// the transform rocks around its own pivot, driven by the global wind
/// (<see cref="WindDriver"/>) — the object itself never deforms.
///
/// This is the third leg of the wind system:
///   • fabric ripple  → shader vertex sway (Coexistence material, _WindMode Hanging/Standing)
///   • loose rope     → DecorativeRope (Verlet physics)
///   • rigid hanging  → THIS component (rotation only)
///
/// SETUP: put the component on the object whose PIVOT is the hook/attachment
/// point (parent an offset GameObject if the mesh pivot is centered).
///
/// DIRECTION RESTRICTION (2026-07-24): by default it rocks toward whichever way the wind blows
/// (<see cref="SwayMode.WindDirectional"/>), which can swing a lantern straight into the pillar it
/// hangs off. Switch to <see cref="SwayMode.FixedPlane"/> and set <see cref="_swayAxis"/> parallel to
/// the wall so the swing runs ALONGSIDE the mount, never into it; <see cref="_oneSided"/> restricts it
/// to a single side of rest. The Scene-view gizmo (select the object) draws the pivot, rest arm, swing
/// arc at ±MaxAngle, and the sway direction — so the angles are tunable by eye.
///
/// All timers scaled time — sways freeze correctly in pause/Setsuna (R10).
/// </summary>
public class WindSwayPivot : MonoBehaviour
{
    public enum SwayMode
    {
        WindDirectional,  // rocks toward whichever way the wind blows (can swing into a nearby mount)
        FixedPlane        // rocks only along one chosen axis — set it parallel to the wall it hangs off
    }

    [Tooltip("Maximum swing angle in degrees at wind strength 1.")]
    [SerializeField] private float _maxAngle = 8f;

    [Tooltip("Swing speed multiplier (pendulum frequency).")]
    [SerializeField] private float _frequency = 1.1f;

    [Tooltip("How much the gust channel adds on top of the base wind.")]
    [Range(0f, 2f)]
    [SerializeField] private float _gustResponse = 0.8f;

    [Header("Direction restriction (stop it swinging into the mount)")]
    [SerializeField] private SwayMode _mode = SwayMode.WindDirectional;
    [Tooltip("FixedPlane only: the LOCAL horizontal axis the object swings ALONG (in its rest orientation). " +
             "Set it parallel to the wall/pillar so the swing runs alongside the mount instead of into it.")]
    [SerializeField] private Vector3 _swayAxis = Vector3.forward;
    [Tooltip("FixedPlane only: swing to ONE side of rest only (never rock back toward the mount).")]
    [SerializeField] private bool _oneSided = false;

    [Header("Editor gizmo (visual only)")]
    [Tooltip("Drawn pendulum-arm length in the Scene view — has no effect on the sway.")]
    [SerializeField] private float _gizmoArm = 0.5f;

    private Quaternion _restRotation;
    private float _phase;                          // per-instance offset so a row of lanterns never syncs
    private Vector3 _swayWorldDir = Vector3.forward; // FixedPlane: cached horizontal swing direction

    private void Awake()
    {
        _restRotation = transform.localRotation;
        Vector3 p = transform.position;
        _phase = (p.x * 0.731f + p.z * 1.117f) % (Mathf.PI * 2f);
        _swayWorldDir = ComputeSwayWorldDir();
    }

    // The chosen local sway axis expressed as a stable HORIZONTAL world direction at rest.
    private Vector3 ComputeSwayWorldDir()
    {
        Quaternion restWorld = (transform.parent != null ? transform.parent.rotation : Quaternion.identity)
                             * (Application.isPlaying ? _restRotation : transform.localRotation);
        Vector3 d = restWorld * _swayAxis;
        d.y = 0f;   // pendulum axis is horizontal — flatten so the swing plane stays vertical
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    private void Update()
    {
        WindDriver wind = WindDriver.Instance;
        if (wind == null) return;

        Vector3 w = wind.EvaluateWind(transform.position);   // xz direction * strength (+ local zones)
        float strength = w.magnitude;
        if (strength < 0.0001f)
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _restRotation, Time.deltaTime * 2f);
            return;
        }

        float t = Time.time * _frequency;   // scaled time (R10)
        float osc = Mathf.Sin(t * 1.7f + _phase) * 0.6f + Mathf.Sin(t * 2.9f + _phase * 1.3f) * 0.4f; // ~ -1..1
        float gust = 1f + _gustResponse * 0.2f * Mathf.Sin(t * 0.61f + _phase);
        float amp = _maxAngle * Mathf.Clamp01(strength) * gust;

        if (_mode == SwayMode.FixedPlane)
        {
            // Rock only in the chosen plane — the tilt axis is FIXED (from _swayAxis), so it can never
            // swing toward the mount. One-sided keeps it to the +axis half (0..amp) if the wall is close.
            float angle = _oneSided ? amp * (0.5f + 0.5f * osc) : amp * osc;
            Vector3 axisWorld = Vector3.Cross(Vector3.up, _swayWorldDir);
            transform.localRotation = _restRotation
                * Quaternion.AngleAxis(angle, transform.InverseTransformDirection(axisWorld));
            return;
        }

        // WindDirectional (original behaviour): tilt away from the wind + a small perpendicular wobble.
        float swing = 0.55f + 0.45f * osc;
        Vector3 dir = w / strength;
        Vector3 swingAxis = Vector3.Cross(Vector3.up, dir);
        float wobble = Mathf.Sin(t * 2.3f + _phase * 2.1f) * _maxAngle * 0.25f * Mathf.Clamp01(strength);

        transform.localRotation = _restRotation
            * Quaternion.AngleAxis(amp * swing, transform.InverseTransformDirection(swingAxis))
            * Quaternion.AngleAxis(wobble, transform.InverseTransformDirection(dir));
    }

    // Scene-view visualisation (editor-only at runtime — Gizmos no-op in builds). Shows the pivot,
    // rest arm, the ±MaxAngle swing extremes and the sway direction so angles/side are tunable by eye.
    private void OnDrawGizmosSelected()
    {
        Vector3 pivot = transform.position;
        float arm = Mathf.Max(0.05f, _gizmoArm);
        Vector3 down = Vector3.down * arm;

        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawWireSphere(pivot, arm * 0.1f);           // pivot marker
        Gizmos.DrawLine(pivot, pivot + down);               // rest arm (hangs straight down)

        Vector3 sd = ComputeSwayWorldDir();
        Vector3 axis = Vector3.Cross(Vector3.up, sd);
        float from = (_mode == SwayMode.FixedPlane && _oneSided) ? 0f : -_maxAngle;

        // swing arc between the extremes
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        const int seg = 12;
        Vector3 prev = pivot + Quaternion.AngleAxis(from, axis) * down;
        for (int i = 1; i <= seg; i++)
        {
            float a = Mathf.Lerp(from, _maxAngle, i / (float)seg);
            Vector3 cur = pivot + Quaternion.AngleAxis(a, axis) * down;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        Gizmos.DrawLine(pivot, pivot + Quaternion.AngleAxis(_maxAngle, axis) * down);   // +extreme arm
        Gizmos.DrawLine(pivot, pivot + Quaternion.AngleAxis(from, axis) * down);        // -extreme (or rest)

        // sway direction (horizontal) — set this parallel to the wall in FixedPlane mode
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        Gizmos.DrawLine(pivot, pivot + sd * arm * 0.6f);
    }
}
