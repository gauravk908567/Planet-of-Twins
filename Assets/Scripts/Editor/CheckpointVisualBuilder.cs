using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;

/// <summary>
/// Builds (or rebuilds) the per-node visuals on <c>DualCheckpoint.prefab</c> (game.md §11.2 "Visual"): under each
/// <see cref="CheckpointNode"/> a "Trails" ParticleSystem (spiral trails in both clan colours, trails-only render) and a
/// "HoldRing" world-space canvas (the shared dual-clan <see cref="UIRingTimerView"/> ring, billboarded, around the
/// orb), plus a wired <see cref="CheckpointNodeVisual"/> on the node. Idempotent: re-running replaces "Trails" and
/// "HoldRing" and re-wires, leaving everything else (orb, prompt, colliders, the user's orb colours) untouched.
/// Tune values here and re-run, or tune the prefab directly afterwards.
/// </summary>
public static class CheckpointVisualBuilder
{
    private const string PrefabPath   = "Assets/Models/Prefabs/Environment/DualCheckpoint.prefab";
    private const string TrailMatPath = "Assets/Shader/Checkpoint/M_CheckpointTrail.mat";
    private const string RingMatPath  = "Assets/Art/Materials/UI/M_UIRingTimer_Shared.mat";   // dual-clan ring, used unmodified
    private const string TrailsName   = "Trails";
    private const string RingName     = "HoldRing";

    // ArtStyle.md §10 mid tones, pushed into HDR so the trails catch bloom. Luminari = Lyra, Vethara = Kai.
    private static readonly Color LuminariGold  = new Color(2.0f, 1.6f, 0.64f, 1f);   // #FFCE52 × 2
    private static readonly Color VetharaViolet = new Color(1.3f, 0.9f, 1.9f, 1f);    // #A874F0 × 2

    private const float OrbParticleSize = 0.5f;   // the orb's "Set Size" in CheckpointOrb.vfx

    [MenuItem("Planet of Twins Tools/Checkpoint/Build Dual Checkpoint Node Visuals")]
    public static void Build()
    {
        var trailMat = AssetDatabase.LoadAssetAtPath<Material>(TrailMatPath);
        var ringMat  = AssetDatabase.LoadAssetAtPath<Material>(RingMatPath);
        if (trailMat == null || ringMat == null)
        {
            Debug.LogError($"[CheckpointVisualBuilder] Missing material: trail={trailMat != null} ring={ringMat != null}.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var dc = root.GetComponent<DualCheckpoint>();
            if (dc == null) { Debug.LogError("[CheckpointVisualBuilder] Prefab root has no DualCheckpoint."); return; }

            int built = 0;
            foreach (var node in root.GetComponentsInChildren<CheckpointNode>(true))
                if (BuildNode(dc, node, trailMat, ringMat)) built++;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[CheckpointVisualBuilder] Built visuals on {built} node(s) of {PrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool BuildNode(DualCheckpoint dc, CheckpointNode node, Material trailMat, Material ringMat)
    {
        var orb = node.GetComponentInChildren<VisualEffect>(true);
        if (orb == null) { Debug.LogError($"[CheckpointVisualBuilder] Node '{node.name}' has no orb VisualEffect.", node); return false; }

        float orbRadius = 0.5f * OrbParticleSize * orb.transform.lossyScale.x;   // world metres
        Vector3 orbPos = orb.transform.position;

        var trails = BuildTrails(node.transform, orbPos, orbRadius, trailMat);
        var (ringRoot, ringView) = BuildRing(node.transform, orbPos, orbRadius, ringMat);

        var visual = node.GetComponent<CheckpointNodeVisual>();
        if (visual == null) visual = node.gameObject.AddComponent<CheckpointNodeVisual>();
        var so = new SerializedObject(visual);
        so.FindProperty("_checkpoint").objectReferenceValue   = dc;
        so.FindProperty("_node").objectReferenceValue         = node;
        so.FindProperty("_orb").objectReferenceValue          = orb;
        so.FindProperty("_trails").objectReferenceValue       = trails;
        so.FindProperty("_holdRingRoot").objectReferenceValue = ringRoot;
        so.FindProperty("_holdRing").objectReferenceValue     = ringView;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static ParticleSystem BuildTrails(Transform node, Vector3 orbPos, float orbRadius, Material trailMat)
    {
        ReplaceChild(node, TrailsName);
        var go = new GameObject(TrailsName);
        go.transform.SetParent(node, false);
        go.transform.position = orbPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);   // variable lifespan
        main.startSpeed = 0f;                                               // motion comes from the orbit + rise below
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);      // = trail width (sizeAffectsWidth)
        main.startColor = new ParticleSystem.MinMaxGradient(LuminariGold, VetharaViolet);   // both clans, random each
        main.maxParticles = 7;                                              // at most 7 alive (CheckpointNodeVisual raises it for the save burst)
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;                                         // script-driven (CheckpointNodeVisual)
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;                                               // start on a shell around the orb
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = orbRadius * 1.2f;
        shape.radiusThickness = 0f;

        var vel = ps.velocityOverLifetime;                                  // orbit + rise + slight spread = spiral
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);                     // x/y/z must share a curve mode
        vel.y = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.radial = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);

        var col = ps.colorOverLifetime;                                     // fade in, hold, fade out
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Fade(0.15f, 0.7f));

        var tr = ps.trails;
        tr.enabled = true;
        tr.mode = ParticleSystemTrailMode.PerParticle;
        tr.ratio = 1f;
        tr.lifetime = 0.35f;
        tr.minVertexDistance = 0.03f;
        tr.worldSpace = true;
        tr.dieWithParticles = true;
        tr.sizeAffectsWidth = true;
        tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        tr.colorOverTrail = new ParticleSystem.MinMaxGradient(Fade(0f, 0f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.None;                       // trails only — no particle heads
        r.trailMaterial = trailMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // White gradient with alpha 0 → 1 at fadeIn → 1 until fadeOutStart → 0. fadeIn 0 = starts opaque.
    private static Gradient Fade(float fadeIn, float fadeOutStart)
    {
        var g = new Gradient();
        var alpha = fadeIn > 0f
            ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOutStart), new GradientAlphaKey(0f, 1f) }
            : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
        return g;
    }

    private static (GameObject, UIRingTimerView) BuildRing(Transform node, Vector3 orbPos, float orbRadius, Material ringMat)
    {
        ReplaceChild(node, RingName);
        const float CanvasUnits = 100f;
        float worldDiameter = orbRadius * 2f * 1.7f;                        // a halo just outside the orb

        var canvasGo = new GameObject(RingName, typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(node, false);
        var rt = (RectTransform)canvasGo.transform;
        rt.position = orbPos;
        rt.sizeDelta = new Vector2(CanvasUnits, CanvasUnits);
        float parentScale = Mathf.Max(0.0001f, node.lossyScale.x);
        rt.localScale = Vector3.one * (worldDiameter / CanvasUnits / parentScale);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<WorldSpaceCanvasCamera>();                    // R9
        var billboard = canvasGo.AddComponent<UIBillboard>();               // face the camera: pitch + yaw
        var bso = new SerializedObject(billboard);
        bso.FindProperty("_lockX").boolValue = true;
        bso.FindProperty("_lockY").boolValue = true;
        bso.ApplyModifiedPropertiesWithoutUndo();

        var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(canvasGo.transform, false);
        var rrt = (RectTransform)ringGo.transform;
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;
        var img = ringGo.GetComponent<Image>();
        img.material = ringMat;
        img.color = Color.white;          // UIRingTimerView: a non-white Image.color corrupts the fill colours
        img.raycastTarget = false;
        var view = ringGo.AddComponent<UIRingTimerView>();

        canvasGo.SetActive(false);        // CheckpointNodeVisual shows it while both twins are on their nodes
        return (canvasGo, view);
    }

    private static void ReplaceChild(Transform parent, string name)
    {
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }
}
