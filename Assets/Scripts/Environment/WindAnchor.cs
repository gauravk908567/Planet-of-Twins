using UnityEngine;

/// <summary>
/// Per-object authoring for the PoT/Coexistence vertex wind sway. The shader's wind fields
/// (<c>_WindEnable/_WindMode/_WindPivotY/_WindMaxAngle/_WindAmount/_WindResponse</c>) live in the
/// per-MATERIAL CBUFFER, so a shared material would sway every instance identically. This component
/// overrides them for THIS renderer via a <see cref="MaterialPropertyBlock"/>, making the attachment
/// point + swing envelope specific to one placed object — no shader change, and no component means the
/// material's own values are used unchanged (fail-safe).
///
/// Model (see CoexistenceCommon.hlsl → PoTApplyWind): the free side ROTATES about the attachment plane
/// by up to <see cref="_maxAngle"/> degrees (the editor draws this cone). We do NOT collide — the
/// designer tunes the angle so the object cannot reach its neighbour; clearance is baked at author time
/// instead of simulated. <see cref="_additiveMetres"/> layers a small positional flutter on top for a
/// natural feel.
///
/// Cost note: a MaterialPropertyBlock override of a CBUFFER field disables SRP batching for THIS
/// renderer. Fine for props (lanterns/banners/signs — dozens). For dense foliage (thousands) bake the
/// sway weight into the mesh instead of adding this component per blade.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("PoT/Wind Anchor")]
public class WindAnchor : MonoBehaviour
{
    public enum SwayMode { Standing = 0, Hanging = 1 }

    /// <summary>Full swing aperture the object may rotate through (see <see cref="Aperture"/>).</summary>
    public enum SwayArc { Free_360 = 0, Half_180 = 1, Narrow_60 = 2, Custom = 3 }

    [Tooltip("Off writes _WindEnable=0 for this object only — it goes still while everything around it sways.")]
    [SerializeField] private bool _windEnabled = true;

    [Tooltip("Standing: base fixed, the part ABOVE the anchor swings. Hanging: top fixed, the part BELOW swings.")]
    [SerializeField] private SwayMode _mode = SwayMode.Standing;

    [Tooltip("Attachment height in MESH-LOCAL space (object-space Y). Drag the scene handle to set it.")]
    [SerializeField] private float _pivotY = 0f;

    [Tooltip("Swing arc preset — the FULL cone the object can rotate through at full wind. " +
             "360 = free spin, 180 = hemisphere, 60 = narrow. The scene cone shows it; tune to clear neighbours.")]
    [SerializeField] private SwayArc _arc = SwayArc.Narrow_60;

    [Tooltip("Full swing aperture in degrees, used only when Arc = Custom.")]
    [SerializeField, Range(0f, 360f)] private float _customAngle = 60f;

    [Tooltip("Additive positional flutter (metres) layered on the swing for a natural feel. Keep small.")]
    [SerializeField, Range(0f, 1f)] private float _additiveMetres = 0.06f;

    [Tooltip("Bend stiffness base→tip: high = bend concentrated near the tip; low = the whole length bends.")]
    [SerializeField, Range(0.05f, 5f)] private float _stiffness = 1f;

    [Tooltip("Renderer to drive. Empty = the Renderer on this object, else the first in children.")]
    [SerializeField] private Renderer _target;

    private MaterialPropertyBlock _mpb;

    private static readonly int ID_Enable   = Shader.PropertyToID("_WindEnable");
    private static readonly int ID_Mode     = Shader.PropertyToID("_WindMode");
    private static readonly int ID_PivotY   = Shader.PropertyToID("_WindPivotY");
    private static readonly int ID_MaxAngle = Shader.PropertyToID("_WindMaxAngle");
    private static readonly int ID_Amount   = Shader.PropertyToID("_WindAmount");
    private static readonly int ID_Response = Shader.PropertyToID("_WindResponse");

    // ── Editor accessors (scene handle + gizmo read these; setter re-applies live) ──
    public SwayMode Mode => _mode;

    /// <summary>Full swing aperture in degrees — the 360/180/60 preset, or the custom value.</summary>
    public float Aperture => _arc switch
    {
        SwayArc.Free_360 => 360f,
        SwayArc.Half_180 => 180f,
        SwayArc.Narrow_60 => 60f,
        _ => _customAngle,
    };

    public float AdditiveMetres => _additiveMetres;
    public bool WindEnabled => _windEnabled;
    public Renderer Target => ResolveTarget();
    public float PivotY
    {
        get => _pivotY;
        set { _pivotY = value; Apply(); }
    }

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private Renderer ResolveTarget()
    {
        if (_target != null) return _target;
        _target = GetComponent<Renderer>();
        if (_target == null) _target = GetComponentInChildren<Renderer>();
        return _target;
    }

    /// <summary>Push the current values into the target renderer's MaterialPropertyBlock.</summary>
    public void Apply()
    {
        Renderer r = ResolveTarget();
        if (r == null) return;   // silently no-op in edit mode; a prop with no renderer just has nothing to sway

        _mpb ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ID_Enable,   _windEnabled ? 1f : 0f);
        _mpb.SetFloat(ID_Mode,     (float)_mode);
        _mpb.SetFloat(ID_PivotY,   _pivotY);
        _mpb.SetFloat(ID_MaxAngle, Aperture * 0.5f);   // shader rotates +/- half-angle; preset is the full aperture
        _mpb.SetFloat(ID_Amount,   _additiveMetres);
        _mpb.SetFloat(ID_Response, _stiffness);
        r.SetPropertyBlock(_mpb);
    }
}
