using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Game-camera-only fullscreen blit feature that ALSO binds the main-light shadow map (plus all
/// URP global textures) so the pass material can sample realtime shadows. This is the piece the
/// stock FullScreenPassRendererFeature does NOT provide — which is exactly why
/// PoT/VolumetricFog's per-step MainLightRealtimeShadow silently returned 1.0 (fully lit) and no
/// light shafts were ever carved (2026-07-24 diagnosis).
///
/// The binding pattern mirrors CristianQiu/Unity-URP-Volumetric-Light's RenderGraph pass:
///   builder.UseTexture(resourceData.mainShadowsTexture)  → makes _MainLightShadowmapTexture live.
/// Combined with UseAllGlobalTextures(true) (shadow matrices/params) and the shader's existing
/// `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS ...`, the stock MainLightRealtimeShadow() now
/// returns real occlusion at each march step.
///
/// Runs only for Game cameras — scene-view depth is unreliable for depth-reconstruction passes in
/// this project (same reason as GameCameraFullScreenFeature; the "transparent buildings" bug).
///
/// Reuses the same inspector surface as the stock feature: a pass material + injection point, and
/// the shader must be Blit-based (uses Blit.hlsl `Vert` and samples _BlitTexture) — PoT/VolumetricFog is.
/// </summary>
public class GameCameraShadowFullScreenFeature : ScriptableRendererFeature
{
    [Tooltip("Blit-based fullscreen material (e.g. M_PoTVolumetricFog).")]
    [SerializeField] private Material passMaterial;

    [Tooltip("When the pass runs. Fog wants After Opaques + Sky / Before Transparents so the " +
             "opaque depth + main shadow map are available.")]
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

    [SerializeField] private int passIndex = 0;

    private ShadowFullScreenPass _pass;

    public override void Create()
    {
        _pass = new ShadowFullScreenPass(passMaterial, passIndex) { renderPassEvent = injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (passMaterial == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;   // scene view depth unreliable

        // Need scene depth (world reconstruction) + camera colour (blit source).
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(_pass);
    }

    private class ShadowFullScreenPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private readonly int _passIndex;

        public ShadowFullScreenPass(Material material, int passIndex)
        {
            _material = material;
            _passIndex = passIndex;
        }

        private class PassData
        {
            public Material material;
            public int passIndex;
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            // Can't read+write the same target — the backbuffer can't be sampled, bail if that's active.
            if (resourceData.isActiveTargetBackBuffer) return;

            // The camera colour target we composite fog back into. Under MSAA (this project runs
            // MSAA2x) this is a MULTISAMPLED attachment, and later geometry — the transparents pass —
            // still renders into it afterwards. So we must NOT replace this handle: reassigning
            // resourceData.cameraColor to a fresh single-sample texture is exactly what produced
            //   "DrawTransparentObjects ... Mismatch in number of MSAA samples ... Expected 2 but got None"
            // every frame (2026-07-24). Instead we copy it, process the copy, and write the fog
            // result straight back into this same MSAA target — the pattern the stock
            // FullScreenPassRendererFeature uses internally.
            TextureHandle cameraColor = resourceData.activeColorTexture;

            // A resolved, single-sample copy of the scene the fog shader samples as its colour input
            // (via _BlitTexture). Single-sample because a material shader can't sample an MSAA texture.
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            TextureHandle sceneCopy =
                UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_PoTFogSceneCopy", false);

            // Pass 1 — plain copy of the camera colour into the single-sample scene copy. RenderGraph
            // resolves the MSAA target as part of this read. No material: the built-in blit copy.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("PoT Fog Copy Colour", out var passData))
            {
                passData.source = cameraColor;
                builder.UseTexture(cameraColor);
                builder.SetRenderAttachment(sceneCopy, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                });
            }

            // Pass 2 — render the fog material (sampling sceneCopy) straight back into the ORIGINAL
            // camera colour target. This is the pass that must see the main shadow map so the
            // shader's MainLightRealtimeShadow() carves real light shafts.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("PoT Shadowed Fog", out var passData))
            {
                passData.material = _material;
                passData.passIndex = _passIndex;
                passData.source = sceneCopy;

                builder.UseTexture(sceneCopy);
                // Declare the shadow map as a read dependency so RenderGraph schedules the shadow
                // pass BEFORE us and doesn't cull it. The actual global binding the stock
                // MainLightRealtimeShadow() macro samples (_MainLightShadowmapTexture + matrices)
                // comes from UseAllGlobalTextures(true) below — both are required for a shader that
                // uses URP's stock shadow macros rather than a hand-bound sampler.
                if (resourceData.mainShadowsTexture.IsValid()) builder.UseTexture(resourceData.mainShadowsTexture);
                if (resourceData.cameraDepthTexture.IsValid()) builder.UseTexture(resourceData.cameraDepthTexture);
                builder.UseAllGlobalTextures(true);   // bind _MainLightShadowmapTexture + shadow matrices/params

                // WriteAll = the full-screen composite overwrites every pixel (sceneCopy already holds
                // the scene), so RenderGraph skips loading the target's prior contents.
                builder.SetRenderAttachment(cameraColor, 0, AccessFlags.WriteAll);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    // Vector2.one = full-viewport scale (the viewportScale overload, matching CristianQiu's pass).
                    Blitter.BlitTexture(ctx.cmd, data.source, Vector2.one, data.material, data.passIndex);
                });
            }

            // NOTE: resourceData.cameraColor is intentionally NOT reassigned — transparents and
            // post-processing keep using the real (MSAA) camera target we just wrote into.
        }
    }
}
