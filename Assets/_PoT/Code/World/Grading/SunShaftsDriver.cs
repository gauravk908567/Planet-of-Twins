using UnityEngine;

/// <summary>
/// Persistent singleton — feeds the PoT/SunShafts fullscreen material its per-frame inputs:
/// the sun's SCREEN-space UV (projected from the main directional light's direction) and a
/// visibility factor (fades the shafts out as the sun leaves the view or drops below the
/// horizon — a radial gather with an off-screen anchor smears garbage otherwise).
/// The corruption dim lives in the shader itself (reads the _WorldCorruption global).
/// R3 Persistent resident; R4: camera resolved via Camera.main each frame (the Brain owns which
/// camera is live — never cached, switch-proof).
/// </summary>
public class SunShaftsDriver : MonoBehaviour
{
    public static SunShaftsDriver Instance { get; private set; }

    [Tooltip("The SunShafts fullscreen material (M_SunShafts) used by the CoexistenceShafts renderer feature.")]
    [SerializeField] private Material _shaftsMaterial;

    [Tooltip("Optional explicit sun. Empty = RenderSettings.sun (the scene's directional light).")]
    [SerializeField] private Light _sun;

    [Tooltip("Extra fade as the sun approaches the screen edge (1 = hard cut at edge).")]
    [SerializeField, Range(0.5f, 4f)] private float _edgeFade = 1.5f;

    private static readonly int SunUVID = Shader.PropertyToID("_SunUV");
    private static readonly int SunVisibilityID = Shader.PropertyToID("_SunVisibility");

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // R3 duplicate guard
        Instance = this;
        if (_shaftsMaterial == null)
        {
            Debug.LogError("[SunShaftsDriver] Shafts material unwired — god rays disabled.", this);   // fail loud
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (_shaftsMaterial != null) _shaftsMaterial.SetFloat(SunVisibilityID, 0f);   // never leak stale shafts
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        var sun = _sun != null ? _sun : RenderSettings.sun;
        if (cam == null || sun == null) { _shaftsMaterial.SetFloat(SunVisibilityID, 0f); return; }

        // Project a far point along the sun's incoming direction to screen space.
        Vector3 sunWorldDir = -sun.transform.forward;
        Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + sunWorldDir * 1000f);

        float vis = 0f;
        if (vp.z > 0f)   // in front of the camera
        {
            // Fade toward the screen edges so shafts never pop when the sun exits the frame.
            float dx = Mathf.Clamp01(1f - Mathf.Abs(vp.x - 0.5f) * _edgeFade);
            float dy = Mathf.Clamp01(1f - Mathf.Abs(vp.y - 0.5f) * _edgeFade);
            vis = dx * dy;
        }

        _shaftsMaterial.SetVector(SunUVID, new Vector4(vp.x, vp.y, 0f, 0f));
        _shaftsMaterial.SetFloat(SunVisibilityID, vis);
    }
}
