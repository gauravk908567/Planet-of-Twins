using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene authoring for <see cref="WindAnchor"/>: a draggable handle on the attachment plane plus a
/// swing-cone gizmo that shows the FULL aperture (60/180/360 preset or custom) the object can rotate
/// through. Tune the anchor + arc so the cone clears the object's neighbours — that clearance is what
/// replaces collision (see PoTApplyWind).
/// </summary>
[CustomEditor(typeof(WindAnchor))]
[CanEditMultipleObjects]
public class WindAnchorEditor : Editor
{
    private static readonly Color AnchorColor = new Color(0.30f, 0.85f, 1.00f, 0.9f);   // attachment plane
    private static readonly Color ConeColor   = new Color(1.00f, 0.62f, 0.20f, 0.9f);   // swing envelope
    private static readonly Color ReachColor  = new Color(1.00f, 0.62f, 0.20f, 0.35f);  // + additive metres

    private void OnSceneGUI()
    {
        var wa = (WindAnchor)target;
        Renderer rend = wa.Target;
        if (rend == null) return;

        Transform t = rend.transform;
        bool standing = wa.Mode == WindAnchor.SwayMode.Standing;
        Vector3 axis = standing ? t.up : -t.up;               // direction the free side extends

        // Object-space mesh bounds → world sizes (upright-prop assumption, same as the shader).
        Bounds b = LocalBounds(rend);
        float scaleY  = Mathf.Abs(t.lossyScale.y);
        float scaleXZ = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z));
        float planeR  = Mathf.Max(b.extents.x, b.extents.z) * scaleXZ;
        float tipY    = standing ? b.max.y : b.min.y;         // free end in mesh-local space
        float leverW  = Mathf.Abs(tipY - wa.PivotY) * scaleY; // world lever arm to the tip

        Vector3 pivotW = t.TransformPoint(new Vector3(0f, wa.PivotY, 0f));

        // ── Attachment plane (the anchored line the object pivots about) ──
        Handles.color = AnchorColor;
        Handles.DrawWireDisc(pivotW, axis, Mathf.Max(planeR, 0.05f));

        // ── Draggable anchor handle (slides along the object's up axis, sets mesh-local pivotY) ──
        float hs = HandleUtility.GetHandleSize(pivotW) * 0.14f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(pivotW, axis, hs, Handles.SphereHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(wa, "Move Wind Anchor");
            wa.PivotY = t.InverseTransformPoint(moved).y;      // back to mesh-local Y
            EditorUtility.SetDirty(wa);
        }

        // ── Swing envelope ──
        float halfDeg = wa.Aperture * 0.5f;
        float halfRad = halfDeg * Mathf.Deg2Rad;

        if (leverW > 1e-3f)
        {
            Handles.color = ConeColor;
            if (halfDeg >= 179f)
            {
                // Free spin — the tip can reach anywhere on a sphere of radius leverW.
                DrawWireSphere(pivotW, leverW, t);
            }
            else
            {
                Vector3 rimCenter = pivotW + axis * (leverW * Mathf.Cos(halfRad));
                float   rimR      = leverW * Mathf.Sin(halfRad);
                Handles.DrawWireDisc(rimCenter, axis, rimR);
                DrawApexLines(pivotW, rimCenter, axis, rimR, 8);
                if (halfDeg > 90f)   // past horizontal: also show the widest sweep (equator)
                    Handles.DrawWireDisc(pivotW, axis, leverW);

                // Additive-metres margin (the flutter can push a touch past the cone).
                if (wa.AdditiveMetres > 1e-3f && rimR > 1e-3f)
                {
                    Handles.color = ReachColor;
                    Handles.DrawWireDisc(rimCenter, axis, rimR + wa.AdditiveMetres);
                }
            }
        }

        Handles.color = Color.white;
        Handles.Label(pivotW + axis * (leverW + 0.1f),
            $"{(wa.WindEnabled ? "" : "(off) ")}swing {wa.Aperture:0}°");
    }

    // Object-space bounds from the mesh; falls back to a unit box.
    private static Bounds LocalBounds(Renderer rend)
    {
        var mf = rend.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds;
        var smr = rend as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.bounds;
        return new Bounds(Vector3.zero, Vector3.one);
    }

    private static void DrawApexLines(Vector3 apex, Vector3 rimCenter, Vector3 axis, float rimR, int count)
    {
        Vector3 a = Vector3.Cross(axis, Vector3.right);
        if (a.sqrMagnitude < 1e-4f) a = Vector3.Cross(axis, Vector3.forward);
        a = a.normalized;
        Vector3 c = Vector3.Cross(axis, a).normalized;
        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f;
            Vector3 p = rimCenter + (a * Mathf.Cos(ang) + c * Mathf.Sin(ang)) * rimR;
            Handles.DrawLine(apex, p);
        }
    }

    private static void DrawWireSphere(Vector3 center, float r, Transform t)
    {
        Handles.DrawWireDisc(center, t.up, r);
        Handles.DrawWireDisc(center, t.right, r);
        Handles.DrawWireDisc(center, t.forward, r);
    }
}
