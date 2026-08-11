using UnityEngine;

/// <summary>
/// Moves an orb (a particle-system root, or any Transform) ALONG a mesh used as a path — the "glowing orb climbs
/// the spiral" effect. A Shuriken particle system can't follow a mesh by itself (no "position by UV/progress"
/// feature), so this samples the mesh into an ORDERED polyline once, then drives the orb's world position by a
/// 0→1 <see cref="progress"/> (0 = bottom, 1 = top). The particle system just rides this moving Transform.
///
/// Path extraction has no single right answer for an arbitrary mesh, so the ORDER is selectable and the result is
/// drawn as a gizmo — pick the mode whose gizmo traces the spiral correctly:
///   • <see cref="PathOrder.VertexIndex"/> — vertices in their stored order (works if the mesh was built along the path).
///   • <see cref="PathOrder.ByHeight"/> — bin vertices along <see cref="upAxis"/> and take each slice's centre,
///     ordered low→high (robust for a spiral that rises monotonically — the common case).
///
/// CONFIG via serialized fields. <see cref="progress"/> is driven externally (e.g. DeathSoulReleaseSequencer) or,
/// for testing, by <see cref="autoPlay"/>. Timing is UNSCALED (a set-piece must not slow under Setsuna — R10).
/// </summary>
[ExecuteAlways]
public class OrbPathFollower : MonoBehaviour
{
    public enum PathOrder { ByUV, ByHeight, VertexIndex }

    [Header("Path")]
    [Tooltip("The mesh whose shape is the path (e.g. spiralmesh). Read from this MeshFilter; points are taken in " +
             "its world space so the orb tracks the mesh if it moves.")]
    [SerializeField] private MeshFilter pathMesh;
    [Tooltip("How to order the mesh vertices into a path. ByUV (default) uses the mesh's authored UV flow as the " +
             "path parameter — correct for a spiral/vortex whose UV runs along it (what an offset shader scrolls). " +
             "ByHeight bins along an axis (only for a simple non-overlapping rise). VertexIndex uses stored order. " +
             "Watch the cyan gizmo and pick the one that traces the spiral.")]
    [SerializeField] private PathOrder order = PathOrder.ByUV;
    [Tooltip("ByUV only: which UV channel runs ALONG the path (X = uv.x, Y = uv.y). Usually X.")]
    [SerializeField] private bool uvUseY = false;
    [Tooltip("ByHeight only: the axis the spiral rises along (local to the mesh).")]
    [SerializeField] private Vector3 upAxis = Vector3.up;
    [Tooltip("ByUV / ByHeight: how many points to sample along the path (more = smoother, slower bake). ~16–32 " +
             "per spiral turn is smooth.")]
    [Min(2)] [SerializeField] private int slices = 200;
    [Tooltip("Auto-flip so progress 0 = the LOWEST point (bottom) and 1 = the highest, regardless of which end " +
             "the extraction started at.")]
    [SerializeField] private bool orientBottomToTop = true;
    [Tooltip("Merge vertices closer than this (m) when ordering by index — collapses a tube's duplicate ring verts.")]
    [Min(0f)] [SerializeField] private float weldDistance = 0.01f;

    [Header("Orb")]
    [Tooltip("The orb to move along the path (the glowing-orb particle system root). Defaults to this Transform.")]
    [SerializeField] private Transform orb;
    [Tooltip("Rotate the orb to face its direction of travel along the path.")]
    [SerializeField] private bool orientToPath = false;

    [Header("Double helix")]
    [Tooltip("Rotate this orb's sampled point around the path's central vertical axis (degrees). Put two orbs on " +
             "the SAME mesh + progress with offsets 0 and 180 → a non-touching double helix (the two strands stay " +
             "diametrically opposite). 0 = follow the mesh exactly.")]
    [SerializeField] private float angularOffset = 0f;

    [Header("Progress (0 = bottom, 1 = top)")]
    [Range(0f, 1f)] public float progress = 0f;
    [Tooltip("Editor/test only: auto-advance progress 0→1 over Duration, then stop. Drive it from code in game.")]
    [SerializeField] private bool autoPlay = false;
    [Min(0.01f)] [SerializeField] private float duration = 3f;

    private Vector3[] _points;     // ordered path points, MESH-LOCAL
    private Vector3 _localCenter;  // centroid of the path (mesh-local) — the helix axis passes through it
    private Matrix4x4 _builtMatrix; // localToWorld at bake time (to detect a moved mesh)
    private float _autoT;

    // When pathMesh references a mesh ASSET (an FBX child, not a live scene object) its transform sits at the
    // world ORIGIN — sampling through it played the helix at (0,0,0) regardless of where the cue spawned
    // (BUG-048: the death helix was never visible). An asset path is a SHAPE reference: play it relative to
    // the orb's spawn position instead. Captured on the first ApplyProgress after enable — by then FxManager's
    // Place() has already positioned the pooled instance.
    private bool _originCaptured;
    private Vector3 _origin;

    private void OnEnable()
    {
        _autoT = 0f;                                        // pool reuse: every play restarts at the bottom
        if (autoPlay && Application.isPlaying) progress = 0f;
        _originCaptured = false;
        Rebuild();
    }

    private void Update()
    {
        if (_points == null || _points.Length < 2) return;

        if (autoPlay && Application.isPlaying)
        {
            _autoT += Time.unscaledDeltaTime / duration;   // unscaled: set-piece must not slow under Setsuna
            progress = Mathf.Clamp01(_autoT);
        }

        ApplyProgress(progress);
    }

    /// <summary>External driver entry point (e.g. the death sequencer). 0 = bottom, 1 = top.</summary>
    public void SetProgress(float t)
    {
        progress = Mathf.Clamp01(t);
        if (_points != null) ApplyProgress(progress);
    }

    /// <summary>Re-extract the path from the mesh. Call after changing the mesh or extraction settings.</summary>
    public void Rebuild()
    {
        if (orb == null) orb = transform;
        _points = ExtractPath();
        if (_points != null && _points.Length > 0)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < _points.Length; i++) c += _points[i];
            _localCenter = c / _points.Length;
        }
        if (pathMesh != null) _builtMatrix = pathMesh.transform.localToWorldMatrix;
    }

    private void ApplyProgress(float t)
    {
        if (orb == null || _points == null || _points.Length == 0) return;

        // Rebake if the mesh moved/scaled since the last bake (points are mesh-local).
        var ltw = pathMesh != null ? pathMesh.transform.localToWorldMatrix : Matrix4x4.identity;

        // Asset-referenced path (FBX child, no live scene transform) = shape only → anchor it at the
        // orb's spawn position, centred so the helix axis rises from the spawn point (see field note).
        bool meshIsAsset = pathMesh != null && !pathMesh.gameObject.scene.IsValid();
        if (meshIsAsset && !_originCaptured)
        {
            _origin = orb.position;
            _originCaptured = true;
        }

        Vector3 localPos = SamplePolyline(t, out Vector3 localDir);
        Vector3 worldPos;
        Vector3 worldCenter;
        if (meshIsAsset)
        {
            Vector3 groundCenter = new Vector3(_localCenter.x, 0f, _localCenter.z);   // keep the mesh's own height profile
            worldPos = _origin + (localPos - groundCenter);
            worldCenter = _origin + (_localCenter - groundCenter);
        }
        else
        {
            worldPos = ltw.MultiplyPoint3x4(localPos);
            worldCenter = ltw.MultiplyPoint3x4(_localCenter);
        }

        // Double-helix offset: rotate this orb around the path's central VERTICAL axis (world up) through the
        // path centre. Two orbs at offsets 0 / 180 stay diametrically opposite → never touch.
        if (Mathf.Abs(angularOffset) > 0.01f)
        {
            Vector3 axisPoint = new Vector3(worldCenter.x, worldPos.y, worldCenter.z);   // axis at this height
            Vector3 radial = worldPos - axisPoint;
            radial = Quaternion.AngleAxis(angularOffset, Vector3.up) * radial;
            worldPos = axisPoint + radial;
        }

        orb.position = worldPos;
        if (orientToPath && localDir.sqrMagnitude > 1e-6f)
            orb.rotation = Quaternion.LookRotation(meshIsAsset ? localDir.normalized
                                                               : ltw.MultiplyVector(localDir).normalized);
    }

    // Sample the ordered polyline at t∈[0,1] by ARC LENGTH (even speed regardless of point spacing).
    private Vector3 SamplePolyline(float t, out Vector3 dir)
    {
        dir = Vector3.forward;
        int n = _points.Length;
        if (n == 1) return _points[0];

        // total length
        float total = 0f;
        for (int i = 0; i < n - 1; i++) total += Vector3.Distance(_points[i], _points[i + 1]);
        if (total <= 1e-6f) return _points[0];

        float target = Mathf.Clamp01(t) * total;
        float acc = 0f;
        for (int i = 0; i < n - 1; i++)
        {
            float seg = Vector3.Distance(_points[i], _points[i + 1]);
            if (acc + seg >= target || i == n - 2)
            {
                float f = seg > 1e-6f ? (target - acc) / seg : 0f;
                dir = (_points[i + 1] - _points[i]);
                return Vector3.Lerp(_points[i], _points[i + 1], Mathf.Clamp01(f));
            }
            acc += seg;
        }
        return _points[n - 1];
    }

    // ── Path extraction ──────────────────────────────────────────────────────────
    private Vector3[] ExtractPath()
    {
        if (pathMesh == null || pathMesh.sharedMesh == null)
        {
            Debug.LogError("[OrbPathFollower] No path mesh assigned — orb can't follow anything.", this);
            return null;
        }
        var mesh = pathMesh.sharedMesh;
        var verts = mesh.vertices;   // mesh-local
        if (verts == null || verts.Length == 0) return null;

        Vector3[] pts;
        switch (order)
        {
            case PathOrder.ByUV:        pts = OrderByUV(verts, mesh.uv); break;
            case PathOrder.ByHeight:    pts = OrderByHeight(verts); break;
            default:                    pts = OrderByVertexIndex(verts); break;
        }

        // Auto-orient so progress 0 = lowest point (bottom). The "up" reference is the world up of the mesh's
        // current transform (so it's correct whatever the FBX import rotation did).
        if (orientBottomToTop && pts != null && pts.Length >= 2)
        {
            Vector3 worldUpLocal = pathMesh.transform.InverseTransformDirection(Vector3.up);
            if (Vector3.Dot(pts[0], worldUpLocal) > Vector3.Dot(pts[pts.Length - 1], worldUpLocal))
                System.Array.Reverse(pts);
        }
        return pts;
    }

    // Bin vertices by the authored UV channel that runs ALONG the path; each bin is a cross-section ring whose
    // centroid is the path point at that position. Correct for a spiral/vortex (the UV is the path parameter).
    private Vector3[] OrderByUV(Vector3[] verts, Vector2[] uv)
    {
        if (uv == null || uv.Length != verts.Length)
        {
            Debug.LogError("[OrbPathFollower] ByUV needs the mesh to have UVs (one per vertex). Falling back to ByHeight.", this);
            return OrderByHeight(verts);
        }
        var sum = new Vector3[slices];
        var count = new int[slices];
        for (int i = 0; i < verts.Length; i++)
        {
            float t = Mathf.Clamp01(uvUseY ? uv[i].y : uv[i].x);
            int b = Mathf.Clamp(Mathf.FloorToInt(t * (slices - 1)), 0, slices - 1);
            sum[b] += verts[i]; count[b]++;
        }
        var result = new System.Collections.Generic.List<Vector3>(slices);
        for (int b = 0; b < slices; b++)
            if (count[b] > 0) result.Add(sum[b] / count[b]);
        return result.ToArray();
    }

    // Vertices in stored order, welding near-duplicates (collapses tube rings to a single point run).
    private Vector3[] OrderByVertexIndex(Vector3[] verts)
    {
        var result = new System.Collections.Generic.List<Vector3>(verts.Length);
        foreach (var v in verts)
            if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], v) > weldDistance)
                result.Add(v);
        return result.ToArray();
    }

    // Bin vertices along upAxis into slices; each slice's centre is one path point, ordered low→high.
    private Vector3[] OrderByHeight(Vector3[] verts)
    {
        Vector3 axis = upAxis.sqrMagnitude < 1e-6f ? Vector3.up : upAxis.normalized;
        float min = float.MaxValue, max = float.MinValue;
        foreach (var v in verts) { float h = Vector3.Dot(v, axis); if (h < min) min = h; if (h > max) max = h; }
        if (max - min < 1e-6f) return new[] { verts[0] };

        var sum = new Vector3[slices];
        var count = new int[slices];
        foreach (var v in verts)
        {
            float h = (Vector3.Dot(v, axis) - min) / (max - min);
            int b = Mathf.Clamp(Mathf.FloorToInt(h * (slices - 1)), 0, slices - 1);
            sum[b] += v; count[b]++;
        }

        var result = new System.Collections.Generic.List<Vector3>(slices);
        for (int b = 0; b < slices; b++)
            if (count[b] > 0) result.Add(sum[b] / count[b]);
        return result.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (slices < 2) slices = 2;
        if (isActiveAndEnabled) Rebuild();
    }

    // Draw the extracted path + the orb's sampled point so you can verify the order mode is right.
    private void OnDrawGizmosSelected()
    {
        if (_points == null || _points.Length < 2) return;
        var ltw = pathMesh != null ? pathMesh.transform.localToWorldMatrix : Matrix4x4.identity;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _points.Length - 1; i++)
            Gizmos.DrawLine(ltw.MultiplyPoint3x4(_points[i]), ltw.MultiplyPoint3x4(_points[i + 1]));

        Gizmos.color = Color.green;                                   // bottom (start)
        Gizmos.DrawSphere(ltw.MultiplyPoint3x4(_points[0]), 0.08f);
        Gizmos.color = Color.red;                                     // top (end)
        Gizmos.DrawSphere(ltw.MultiplyPoint3x4(_points[_points.Length - 1]), 0.08f);

        Gizmos.color = Color.yellow;                                  // current orb sample
        Vector3 p = SamplePolyline(progress, out _);
        Gizmos.DrawWireSphere(ltw.MultiplyPoint3x4(p), 0.12f);
    }
#endif
}
