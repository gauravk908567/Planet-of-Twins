using UnityEngine;

/// <summary>
/// Moves an orb (a particle-system root, or any Transform) along a HELIX between two points — no mesh, pure math.
/// The orb travels start→end as <see cref="progress"/> goes 0→1, spiralling around the travel line; its trail
/// (sub-emitters) draws the helix. Built for Weaver's Gate: the soul's caster→marker trip is shown as this
/// spiralling orb instead of the bare soul.
///
/// "HELIX SPEED" = <see cref="revolutions"/> — how many full turns it makes over the whole travel (one knob).
/// Put two orbs on the same start/end + progress with <see cref="angularOffset"/> 0 and 180 → a non-touching
/// double helix. Endpoints can be Transforms (in-scene authoring) OR set at runtime via <see cref="SetEndpoints"/>
/// (the gate passes caster + marker). Unscaled time (a set-piece must not slow under Setsuna — R10).
/// </summary>
[ExecuteAlways]
public class HelixFollower : MonoBehaviour
{
    [Header("Endpoints (Transforms for authoring; or SetEndpoints() at runtime)")]
    [SerializeField] private Transform startPoint;   // caster
    [SerializeField] private Transform endPoint;     // marker / target

    [Header("Helix shape")]
    [Tooltip("Distance of the orb from the travel line (the helix radius).")]
    [Min(0f)] [SerializeField] private float radius = 0.5f;
    [Tooltip("HELIX SPEED — full turns made over the whole travel. Higher = tighter/faster spiral.")]
    [SerializeField] private float revolutions = 3f;
    [Tooltip("Radius scale across the travel (0→1). Flat line = constant radius; ease to 0 at the ends = the " +
             "strands meet at caster & marker. Tune freely.")]
    [SerializeField] private AnimationCurve radiusOverTravel = AnimationCurve.Constant(0f, 1f, 1f);
    [Tooltip("Rotate this orb around the travel line (degrees). Two orbs at 0 and 180 → non-touching double helix.")]
    [SerializeField] private float angularOffset = 0f;

    [Header("Progress (0 = caster, 1 = marker)")]
    [Range(0f, 1f)] public float progress = 0f;
    [Tooltip("Editor/test only: auto-advance 0→1 over Duration. In game the gate drives progress to match the soul.")]
    [SerializeField] private bool autoPlay = false;
    [Min(0.01f)] [SerializeField] private float duration = 1.5f;

    [Tooltip("The orb to move (defaults to this Transform).")]
    [SerializeField] private Transform orb;

    // Runtime endpoints (used when the Transforms are null — e.g. the gate sets these each cast).
    private Vector3 _start, _end;
    private bool _hasRuntimeEndpoints;
    private float _autoT;

    private void OnEnable()
    {
        if (orb == null) orb = transform;
        _autoT = 0f;
    }

    private void Update()
    {
        if (autoPlay && Application.isPlaying)
        {
            _autoT += Time.unscaledDeltaTime / duration;
            progress = Mathf.Clamp01(_autoT);
        }
        Apply(progress);
    }

    /// <summary>Gate/sequencer entry point: set the two world endpoints (caster → marker). Overrides the
    /// serialized Transforms until <see cref="ClearRuntimeEndpoints"/>.</summary>
    public void SetEndpoints(Vector3 start, Vector3 end)
    {
        _start = start; _end = end; _hasRuntimeEndpoints = true;
    }
    public void ClearRuntimeEndpoints() => _hasRuntimeEndpoints = false;

    /// <summary>Drive the travel (0 = caster, 1 = marker).</summary>
    public void SetProgress(float t)
    {
        progress = Mathf.Clamp01(t);
        Apply(progress);
    }

    private bool Resolve(out Vector3 s, out Vector3 e)
    {
        if (_hasRuntimeEndpoints) { s = _start; e = _end; return true; }
        if (startPoint != null && endPoint != null) { s = startPoint.position; e = endPoint.position; return true; }
        s = e = Vector3.zero; return false;
    }

    private void Apply(float t)
    {
        if (orb == null) return;
        if (!Resolve(out Vector3 s, out Vector3 e)) return;

        Vector3 baseP = Vector3.Lerp(s, e, t);
        Vector3 axis = e - s;
        float len = axis.magnitude;
        if (len < 1e-4f) { orb.position = baseP; return; }
        axis /= len;

        // A stable basis perpendicular to the travel line.
        Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(axis, up).normalized;
        Vector3 w = Vector3.Cross(axis, u).normalized;

        float ang = (t * revolutions * 360f + angularOffset) * Mathf.Deg2Rad;
        float r = radius * (radiusOverTravel != null ? radiusOverTravel.Evaluate(t) : 1f);
        orb.position = baseP + r * (Mathf.Cos(ang) * u + Mathf.Sin(ang) * w);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Resolve(out Vector3 s, out Vector3 e)) return;
        Gizmos.color = Color.cyan;
        Vector3 prev = SamplePreview(0f, s, e);
        for (int i = 1; i <= 64; i++)
        {
            Vector3 p = SamplePreview(i / 64f, s, e);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.color = Color.green; Gizmos.DrawSphere(s, 0.06f);   // caster
        Gizmos.color = Color.red;   Gizmos.DrawSphere(e, 0.06f);   // marker
    }

    private Vector3 SamplePreview(float t, Vector3 s, Vector3 e)
    {
        Vector3 baseP = Vector3.Lerp(s, e, t);
        Vector3 axis = e - s; float len = axis.magnitude; if (len < 1e-4f) return baseP; axis /= len;
        Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(axis, up).normalized; Vector3 w = Vector3.Cross(axis, u).normalized;
        float ang = (t * revolutions * 360f + angularOffset) * Mathf.Deg2Rad;
        float r = radius * (radiusOverTravel != null ? radiusOverTravel.Evaluate(t) : 1f);
        return baseP + r * (Mathf.Cos(ang) * u + Mathf.Sin(ang) * w);
    }
#endif
}
