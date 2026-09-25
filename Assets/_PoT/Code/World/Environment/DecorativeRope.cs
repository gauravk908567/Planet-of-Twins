using UnityEngine;

/// <summary>
/// DecorativeRope — a hanging celebration rope (shimenawa, lantern lines, bunting) that reacts
/// to the world wind. Same mesh-free vocabulary as <see cref="ChainBeamDriver"/>: a multi-point
/// <see cref="LineRenderer"/> strip, no physics components — but where the chain fakes motion
/// with Perlin wobble, this runs a real VERLET sim (gravity + <see cref="WindDriver.EvaluateWind"/>
/// + distance constraints) so slack, swing and gust response come out naturally.
///
/// SETUP: put this on a GO at the rope's START point, assign the END anchor transform, give the
/// LineRenderer a material + width curve. Slack 1.0 = taut, 1.2 = gentle festival droop.
/// Hang decorations (tassels, shide paper) as separate small props with the Coexistence wind
/// sway (Hanging mode), or sample <see cref="GetPoint"/> to pin objects onto the rope.
///
/// Edit mode ([ExecuteAlways]): draws a static catenary preview so placement is WYSIWYG — the
/// sim only runs in play mode. Sim uses SCALED time clamped to 1/30 s (R10: decoration follows
/// world time — Setsuna slows the swing, pause freezes it; the clamp keeps the Verlet stable
/// after hitches). No cross-scene refs: rope + anchor must live in the same scene (R2).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways]
public class DecorativeRope : MonoBehaviour
{
    [Header("Anchors (start = this transform)")]
    [Tooltip("Rope end point. SAME scene as this object (R2).")]
    [SerializeField] private Transform _endAnchor;

    [Header("Rope")]
    [SerializeField, Range(4, 48)] private int _segments = 20;
    [Tooltip("Rest length = anchor distance × slack. 1 = taut, 1.2 = festival droop.")]
    [SerializeField, Range(1f, 2f)] private float _slack = 1.15f;
    [SerializeField, Min(0f)] private float _gravity = 9.81f;
    [Tooltip("Velocity kept per step (higher = livelier, lower = heavier rope).")]
    [SerializeField, Range(0.8f, 0.999f)] private float _damping = 0.97f;
    [SerializeField, Range(1, 12)] private int _constraintIterations = 6;

    [Header("Wind")]
    [Tooltip("Multiplies WindDriver's force on this rope (heavy shimenawa ≈ 0.3, light bunting ≈ 1.5).")]
    [SerializeField, Min(0f)] private float _windMultiplier = 1f;

    private LineRenderer _line;
    private Vector3[] _pos, _prev;
    private float _segmentLength;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
    }

    private void OnEnable()
    {
        if (Application.isPlaying) InitSim();
    }

    private void InitSim()
    {
        if (_endAnchor == null)
        {
            Debug.LogError("[DecorativeRope] End anchor unassigned — rope disabled.", this);   // fail loud
            enabled = false;
            return;
        }
        int n = _segments + 1;
        _pos = new Vector3[n];
        _prev = new Vector3[n];
        Vector3 a = transform.position, b = _endAnchor.position;
        _segmentLength = Vector3.Distance(a, b) * _slack / _segments;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)_segments;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= CatenarySag(t);
            _pos[i] = p;
            _prev[i] = p;
        }
        _line.positionCount = n;
    }

    // Approximate initial droop so the rope starts settled instead of falling into place.
    private float CatenarySag(float t) =>
        Mathf.Sin(t * Mathf.PI) * Vector3.Distance(transform.position, _endAnchor.position) * (_slack - 1f) * 0.75f;

    private void Update()
    {
        if (!Application.isPlaying)
        {
            EditorPreview();
            return;
        }
        if (_pos == null || _endAnchor == null) return;

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);   // scaled — see class doc
        if (dt <= 0f) return;                              // paused: rope holds its shape

        Vector3 wind = (WindDriver.Instance != null ? WindDriver.Instance.EvaluateWind(transform.position) : Vector3.zero)
                       * _windMultiplier;
        Vector3 accel = Vector3.down * _gravity + wind;

        // Verlet integrate
        for (int i = 0; i < _pos.Length; i++)
        {
            Vector3 vel = (_pos[i] - _prev[i]) * _damping;
            _prev[i] = _pos[i];
            _pos[i] += vel + accel * (dt * dt);
        }

        // Constraints: pin ends to (possibly moving) anchors, keep segment lengths.
        for (int k = 0; k < _constraintIterations; k++)
        {
            _pos[0] = transform.position;
            _pos[_pos.Length - 1] = _endAnchor.position;
            for (int i = 0; i < _pos.Length - 1; i++)
            {
                Vector3 d = _pos[i + 1] - _pos[i];
                float len = d.magnitude;
                if (len < 1e-5f) continue;
                Vector3 corr = d * ((len - _segmentLength) / len * 0.5f);
                if (i != 0) _pos[i] += corr;
                if (i + 1 != _pos.Length - 1) _pos[i + 1] -= corr;
            }
        }

        for (int i = 0; i < _pos.Length; i++) _line.SetPosition(i, _pos[i]);
    }

    /// <summary>World position at t 0..1 along the rope (pin tassels/lanterns onto it at runtime).</summary>
    public Vector3 GetPoint(float t)
    {
        if (_pos == null || _pos.Length == 0) return transform.position;
        float f = Mathf.Clamp01(t) * (_pos.Length - 1);
        int i = Mathf.FloorToInt(f);
        if (i >= _pos.Length - 1) return _pos[_pos.Length - 1];
        return Vector3.Lerp(_pos[i], _pos[i + 1], f - i);
    }

    // Static droop preview in edit mode so the user can place ropes without pressing Play.
    private void EditorPreview()
    {
        if (_line == null) _line = GetComponent<LineRenderer>();
        if (_endAnchor == null) { _line.positionCount = 0; return; }
        int n = _segments + 1;
        if (_line.positionCount != n) _line.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)_segments;
            Vector3 p = Vector3.Lerp(transform.position, _endAnchor.position, t);
            p.y -= CatenarySag(t);
            _line.SetPosition(i, p);
        }
    }
}
