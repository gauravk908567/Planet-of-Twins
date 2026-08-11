using UnityEngine;

/// <summary>
/// EDITOR VISUALISER for the PoT/Coexistence shader's VERTEX wind sway (Standing / Hanging).
/// Those knobs — Wind Mode, Attachment Y, Sway Amount, Mask Falloff — are MATERIAL properties,
/// and a material cannot draw a scene gizmo. Drop this on the prop's renderer to SEE where the
/// shader anchors the mesh and which end actually sways. Pure gizmos: no runtime cost, no build
/// footprint (OnDrawGizmos is editor-only; the one Handles call is #if-guarded).
///
/// Reads the shared material (no play mode needed) and draws, in the renderer's OBJECT space
/// (the shader sways on positionOS.y, so the anchor is an object-space Y):
///   • CYAN rectangle at Attachment Y (_WindPivotY) — the fixed anchor line.
///   • the ANCHORED half dimmed (Standing = below the line, Hanging = above it).
///   • a YELLOW rectangle at 1/_WindResponse metres from the anchor — where sway reaches full.
///   • a side-to-side arrow at the swaying tip (width ≈ _WindAmount, illustrative).
///
/// Property names mirror Coexistence.shader:
///   _WindEnable · _WindMode (0 Standing / 1 Hanging) · _WindPivotY · _WindAmount · _WindResponse
/// This is the shader-sway sibling of <see cref="WindSwayPivot"/> (which is the RIGID-rotation leg
/// and draws its own gizmo). Different mechanisms — this one visualises the shader, not a transform.
/// </summary>
[AddComponentMenu("PoT/Dev/Wind Sway Gizmo (shader sway)")]
public class WindSwayGizmo : MonoBehaviour
{
    [Tooltip("Draw even when the material's Wind Sway toggle is OFF (to place the anchor before enabling it).")]
    [SerializeField] private bool _alwaysDraw = true;
    [Tooltip("Only draw while this object is selected (less scene clutter). Off = always visible.")]
    [SerializeField] private bool _onlyWhenSelected = false;

    static readonly int P_Enable   = Shader.PropertyToID("_WindEnable");
    static readonly int P_Mode     = Shader.PropertyToID("_WindMode");
    static readonly int P_PivotY   = Shader.PropertyToID("_WindPivotY");
    static readonly int P_Amount   = Shader.PropertyToID("_WindAmount");
    static readonly int P_Response = Shader.PropertyToID("_WindResponse");

    private void OnDrawGizmos()         { if (!_onlyWhenSelected) Draw(); }
    private void OnDrawGizmosSelected() { if (_onlyWhenSelected)  Draw(); }

    private void Draw()
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;
        var mat = rend.sharedMaterial;
        if (mat == null || !mat.HasProperty(P_PivotY)) return;

        bool windOn = mat.HasProperty(P_Enable) && mat.GetFloat(P_Enable) > 0.5f;
        if (!windOn && !_alwaysDraw) return;

        int   mode     = mat.HasProperty(P_Mode)     ? Mathf.RoundToInt(mat.GetFloat(P_Mode)) : 0; // 0 Standing, 1 Hanging
        float pivotY   = mat.GetFloat(P_PivotY);
        float amount   = mat.HasProperty(P_Amount)   ? mat.GetFloat(P_Amount) : 0.12f;
        float response = mat.HasProperty(P_Response) ? Mathf.Max(0.05f, mat.GetFloat(P_Response)) : 1f;

        // Object-space bounds — the shader masks on positionOS.y, so work in the renderer's local space.
        Bounds b = rend.localBounds;
        float minX = b.min.x, maxX = b.max.x, minZ = b.min.z, maxZ = b.max.z, minY = b.min.y, maxY = b.max.y;

        // Standing: base fixed, TOP sways (dir +1). Hanging: top fixed, BOTTOM sways (dir -1).
        float dir    = mode == 0 ? +1f : -1f;
        float fullY  = Mathf.Clamp(pivotY + dir * (1f / response), minY, maxY); // where mask saturates to 1
        float tipY   = dir > 0f ? maxY : minY;   // swaying extreme
        float fixedY = dir > 0f ? minY : maxY;   // anchored extreme

        Gizmos.matrix = transform.localToWorldMatrix;

        // Anchor line (bright cyan) — the "Attachment Y".
        Gizmos.color = new Color(0.20f, 0.90f, 1f, windOn ? 1f : 0.5f);
        DrawRect(pivotY, minX, maxX, minZ, maxZ);

        // Anchored (fixed) half — dim cyan rectangle + spine so it reads as "this end doesn't move".
        Gizmos.color = new Color(0.20f, 0.90f, 1f, 0.22f);
        DrawRect(fixedY, minX, maxX, minZ, maxZ);
        Gizmos.DrawLine(new Vector3(b.center.x, pivotY, b.center.z),
                        new Vector3(b.center.x, fixedY, b.center.z));

        // Full-sway line (yellow) — beyond here the vertex offset is maxed at _WindAmount.
        Gizmos.color = new Color(1f, 0.85f, 0.15f, windOn ? 0.9f : 0.45f);
        DrawRect(fullY, minX, maxX, minZ, maxZ);

        // Side-to-side sway arrow at the swaying tip (width ≈ _WindAmount, in metres — illustrative).
        Gizmos.color = new Color(0.55f, 1f, 0.40f, windOn ? 1f : 0.5f);
        Vector3 tip = new Vector3(b.center.x, tipY, b.center.z);
        float a = Mathf.Max(amount, 0.03f);
        float barb = tipY > pivotY ? -1f : 1f;                     // point the arrowheads back toward the anchor
        Gizmos.DrawLine(tip + Vector3.left * a, tip + Vector3.right * a);
        Gizmos.DrawLine(tip + Vector3.right * a, tip + new Vector3( a * 0.6f, barb * a * 0.5f, 0f));
        Gizmos.DrawLine(tip + Vector3.left  * a, tip + new Vector3(-a * 0.6f, barb * a * 0.5f, 0f));

#if UNITY_EDITOR
        UnityEditor.Handles.matrix = transform.localToWorldMatrix;
        UnityEditor.Handles.color = new Color(0.20f, 0.90f, 1f);
        UnityEditor.Handles.Label(new Vector3(maxX, pivotY, b.center.z),
            $"  Attachment Y = {pivotY:0.###}  ({(mode == 0 ? "Standing — top sways" : "Hanging — bottom sways")}){(windOn ? "" : "  [wind OFF]")}");
#endif
    }

    private static void DrawRect(float y, float minX, float maxX, float minZ, float maxZ)
    {
        Vector3 p0 = new Vector3(minX, y, minZ);
        Vector3 p1 = new Vector3(maxX, y, minZ);
        Vector3 p2 = new Vector3(maxX, y, maxZ);
        Vector3 p3 = new Vector3(minX, y, maxZ);
        Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
    }
}
