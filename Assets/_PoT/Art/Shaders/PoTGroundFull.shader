// PoT/GroundFull — a full-PBR surface shader that exposes EVERY map an artist ships,
// including the three URP has no slot for: CAVITY, OPACITY, and FUZZ.
//
// WHY THIS EXISTS: it is a LEARNING / COMPARISON tool, not the production answer.
// Every greyscale map here (ao, cavity, smoothness, height, opacity) costs a separate
// texture fetch per pixel. On a ground surface — which covers a huge share of the screen —
// that is the worst place to pay it. Production practice is to CHANNEL-PACK these down to
// ~3 textures and use stock URP/Lit (see game.md 17.x). Use this shader to decide which
// maps actually earn their keep, then bake the winners into a packed set.
//
// HOW TO USE IT: every map has its own STRENGTH slider. Set a strength to 0 to switch that
// map off and see the surface without it; slide to 1 for full contribution. That A/B is the
// whole point — judge each map instead of taking anyone's word for it.
//
// NOTE ON ROUGHNESS vs GLOSSINESS: there is ONE smoothness slot. Plug in either map and use
// the Invert toggle — they are the same data, one is 1-minus the other. Proving that to
// yourself is worth thirty seconds.
Shader "PoT/GroundFull"
{
    Properties
    {
        [Header(Base)]
        _BaseMap        ("Albedo", 2D) = "white" {}
        _BaseColor      ("Base Tint", Color) = (1,1,1,1)

        [Header(Normal)]
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale    ("Normal Strength", Range(0,4)) = 1.0

        [Header(Smoothness   plug in glossiness OR roughness)]
        _SmoothnessMap  ("Smoothness / Roughness Map", 2D) = "white" {}
        [Toggle(_SMOOTHNESS_INVERT)] _SmoothnessInvert ("Map is ROUGHNESS (invert it)", Float) = 0
        _SmoothnessMapStrength ("Map Strength (0 = use slider only)", Range(0,1)) = 1.0
        _Smoothness     ("Smoothness Multiplier", Range(0,1)) = 1.0

        [Header(Metallic   or Specular workflow)]
        _Metallic       ("Metallic", Range(0,1)) = 0.0
        [Toggle(_SPECULAR_SETUP)] _SpecularSetup ("Use SPECULAR workflow", Float) = 0
        _SpecularMap    ("Specular Map (specular workflow only)", 2D) = "white" {}
        _SpecColor      ("Specular Tint", Color) = (0.2,0.2,0.2,1)

        [Header(Ambient Occlusion)]
        _OcclusionMap   ("AO Map", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0,1)) = 1.0

        [Header(Cavity   micro occlusion   URP has no slot for this)]
        // Cavity is tight, high-frequency occlusion: pores, scratches, chisel marks — as
        // opposed to AO, which is broad and soft. In production you MULTIPLY it into albedo
        // offline and delete the map. Here it is live so you can see what it contributes.
        _CavityMap      ("Cavity Map", 2D) = "white" {}
        _CavityAlbedo   ("Cavity into ALBEDO", Range(0,1)) = 0.3
        _CavityRough    ("Cavity into ROUGHNESS (dirt sits in pores)", Range(0,1)) = 0.2

        [Header(Fuzz   grazing angle sheen   moss fibre detail   URP has no slot for this)]
        // Fuzz masks WHERE fine fibrous micro-geometry lives (moss, lichen, fur) — stuff too
        // small for the normal map to fake. It doesn't feed the normal or roughness; it adds
        // its own soft rim-light term (a Fresnel/sheen glow at grazing angles) gated by the
        // mask, on top of the normal PBR result. Intensity 0 = off, so it's the same A/B
        // control as every other slider here.
        _FuzzMap        ("Fuzz Mask", 2D) = "black" {}
        _FuzzColor      ("Fuzz Tint", Color) = (1,1,1,1)
        _FuzzIntensity  ("Fuzz Intensity", Range(0,3)) = 1.0
        _FuzzPower      ("Fuzz Grazing Falloff", Range(0.5,8)) = 3.0

        [Header(Height   parallax)]
        _HeightMap      ("Height / Displacement", 2D) = "black" {}
        _HeightScale    ("Parallax Amount", Range(0,0.2)) = 0.02

        [Header(Opacity   URP has no slot for this either)]
        // URP reads alpha from the BASE map's alpha channel. A separate opacity texture (as
        // most artists ship) has nowhere to go — hence this slot. Only matters with Alpha
        // Clip on; for a solid ground tile leave it off and the map is dead weight.
        _OpacityMap     ("Opacity Map", 2D) = "white" {}
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (cutout)", Float) = 0
        _Cutoff         ("Cutout Threshold", Range(0,1)) = 0.5

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Shared declarations for every pass.
        // ─────────────────────────────────────────────────────────────────────────────
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _SpecColor;
            half   _NormalScale;
            half   _SmoothnessMapStrength;
            half   _Smoothness;
            half   _Metallic;
            half   _OcclusionStrength;
            half   _CavityAlbedo;
            half   _CavityRough;
            half   _HeightScale;
            half   _Cutoff;
            half4  _FuzzColor;
            half   _FuzzIntensity;
            half   _FuzzPower;
        CBUFFER_END

        TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
        TEXTURE2D(_SmoothnessMap);  SAMPLER(sampler_SmoothnessMap);
        TEXTURE2D(_SpecularMap);    SAMPLER(sampler_SpecularMap);
        TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_CavityMap);      SAMPLER(sampler_CavityMap);
        TEXTURE2D(_HeightMap);      SAMPLER(sampler_HeightMap);
        TEXTURE2D(_OpacityMap);     SAMPLER(sampler_OpacityMap);
        TEXTURE2D(_FuzzMap);        SAMPLER(sampler_FuzzMap);

        // Opacity lives in its own texture here (URP would want it in _BaseMap.a).
        half SampleAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, uv).r;
        }
        ENDHLSL

        // ─────────────────────────────────────────────────────────────────────────────
        // FORWARD LIT — the pass that does the actual shading.
        // ─────────────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag

            // Material feature keywords
            #pragma shader_feature_local_fragment _SMOOTHNESS_INVERT
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // URP lighting keywords — omitting these is how a custom shader ends up with
            // no shadows / no additional lights / no fog and looks "wrong" for no reason.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                half4  tangentWS   : TEXCOORD3;   // w = bitangent sign
                half3  viewDirTS   : TEXCOORD4;   // for parallax
                half4  fogFactorAndVertexLight : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
                // APV (Adaptive Probe Volumes) needs per-pixel probe occlusion for shadowmask
                // blending — this project runs APV (game.md), so this interpolator is required,
                // not optional, or SAMPLE_GI's probe-volume overload has nothing to read.
                float4 probeOcclusion : TEXCOORD7;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD8;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ForwardVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS   = nrm.normalWS;
                OUT.tangentWS  = half4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());

                // View direction in TANGENT space — parallax needs it there, not world space.
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(pos.positionWS);
                half3 bitangent = OUT.tangentWS.w * cross(nrm.normalWS, nrm.tangentWS);
                OUT.viewDirTS = half3(
                    dot(nrm.tangentWS, viewDirWS),
                    dot(bitangent,     viewDirWS),
                    dot(nrm.normalWS,  viewDirWS));

                OUT.fogFactorAndVertexLight = half4(
                    ComputeFogFactor(pos.positionCS.z),
                    VertexLighting(pos.positionWS, nrm.normalWS));

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH4(pos.positionWS, OUT.normalWS.xyz, GetWorldSpaceNormalizeViewDir(pos.positionWS),
                    OUT.vertexSH, OUT.probeOcclusion);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    OUT.shadowCoord = GetShadowCoord(pos);
                #endif
                return OUT;
            }

            half4 ForwardFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ── HEIGHT: offset the UVs before anything else samples them ──────────
                float2 uv = IN.uv;
                if (_HeightScale > 0.0001)
                {
                    half h = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uv).r;
                    half3 vTS = normalize(IN.viewDirTS);
                    // Single-step parallax: cheap, and enough to judge the map's value.
                    uv += (h - 0.5) * _HeightScale * (vTS.xy / max(vTS.z, 0.2));
                }

                // ── OPACITY ───────────────────────────────────────────────────────────
                half alpha = SampleAlpha(uv) * _BaseColor.a;
                #ifdef _ALPHATEST_ON
                    clip(alpha - _Cutoff);
                #endif

                // ── ALBEDO + CAVITY ───────────────────────────────────────────────────
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
                half cavity = SAMPLE_TEXTURE2D(_CavityMap, sampler_CavityMap, uv).r;
                // lerp(1, cavity, k) — at k=0 the map is fully off, which is the A/B control.
                albedo *= lerp(1.0h, cavity, _CavityAlbedo);

                // ── SMOOTHNESS (accepts glossiness OR roughness) ──────────────────────
                half sm = SAMPLE_TEXTURE2D(_SmoothnessMap, sampler_SmoothnessMap, uv).r;
                #ifdef _SMOOTHNESS_INVERT
                    sm = 1.0h - sm;         // fed a ROUGHNESS map — same data, inverted
                #endif
                sm = lerp(1.0h, sm, _SmoothnessMapStrength) * _Smoothness;
                // Cavity also roughens: grime collects in pores, so they scatter more.
                sm *= lerp(1.0h, cavity, _CavityRough);

                // ── AO ────────────────────────────────────────────────────────────────
                half ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
                ao = lerp(1.0h, ao, _OcclusionStrength);

                // ── NORMAL ────────────────────────────────────────────────────────────
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);

                // ── Build URP's SurfaceData ───────────────────────────────────────────
                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = albedo;
                surface.alpha      = alpha;
                surface.normalTS   = normalTS;
                surface.smoothness = saturate(sm);
                surface.occlusion  = ao;
                surface.emission   = 0;
                #ifdef _SPECULAR_SETUP
                    surface.metallic = 0;
                    surface.specular = SAMPLE_TEXTURE2D(_SpecularMap, sampler_SpecularMap, uv).rgb * _SpecColor.rgb;
                #else
                    surface.metallic = _Metallic;
                    surface.specular = 0;
                #endif

                // ── Build URP's InputData ─────────────────────────────────────────────
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;

                half3 bitangent = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                inputData.normalWS = TransformTangentToWorld(normalTS, tbn);
                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0,0,0,0);
                #endif

                inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS,1), IN.fogFactorAndVertexLight.x);
                inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                // ── FUZZ: grazing-angle sheen, gated by the mask, added as emission ────
                // Not a real BRDF term — a cheap Fresnel rim tinted by the mask so fuzzy
                // (mossy/fibrous) areas catch light at silhouette angles the way real fine
                // fibres do, without per-fibre geometry. Riding on SurfaceData.emission keeps
                // it additive on top of whatever URP's PBR result already is, no internal
                // lighting-function changes required.
                half fuzzMask = SAMPLE_TEXTURE2D(_FuzzMap, sampler_FuzzMap, uv).r;
                half fuzzNdotV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half fuzzFresnel = pow(1.0h - fuzzNdotV, _FuzzPower);
                surface.emission += fuzzFresnel * fuzzMask * _FuzzIntensity * _FuzzColor.rgb;

                // SAMPLE_GI's argument count depends on which GI path is compiled in — static
                // lightmap vs Adaptive Probe Volumes take different overloads (see
                // PoTTerrainLitPasses.hlsl InitializeBakedGIData for the same branch in this
                // project's other custom-lit shader).
                #if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    inputData.bakedGI = SAMPLE_GI(IN.vertexSH,
                        GetAbsolutePositionWS(IN.positionWS),
                        inputData.normalWS,
                        inputData.viewDirectionWS,
                        inputData.positionCS.xy,
                        IN.probeOcclusion,
                        inputData.shadowMask);
                #else
                    inputData.bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.lightmapUV);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // SHADOW CASTER — without this the material casts no shadows.
        // ─────────────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            SVaryings ShadowVert(SAttributes IN)
            {
                SVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // ApplyShadowBias returns a WORLD position, not clip space — it still has to
                // go through TransformWorldToHClip.
                OUT.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(SVaryings IN) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SampleAlpha(IN.uv) * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // DEPTH ONLY + DEPTH NORMALS — this project's renderer runs SSAO and Decals,
        // both of which need these. Omit them and the ground silently drops out of both.
        // ─────────────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct DAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            DVaryings DepthVert(DAttributes IN)
            {
                DVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthFrag(DVaryings IN) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SampleAlpha(IN.uv) * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  tangentWS  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DNVaryings DepthNormalsVert(DNAttributes IN)
            {
                DNVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = pos.positionCS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS   = nrm.normalWS;
                OUT.tangentWS  = half4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                return OUT;
            }

            half4 DepthNormalsFrag(DNVaryings IN) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SampleAlpha(IN.uv) * _BaseColor.a - _Cutoff);
                #endif
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv), _NormalScale);
                half3 bitangent = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                // DepthNormals wants a world-space normal; NormalizeNormalPerPixel is the only
                // normalisation needed here.
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));
                return half4(normalWS, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
