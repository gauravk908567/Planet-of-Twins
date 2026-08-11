// PoT/VolumetricFog — raymarched volumetric height fog, written for Planet of Twins (2026-07-16).
//
// Technique (studied from public references — Jimenez interleaved-gradient-noise jitter,
// Henyey-Greenstein phase, per-step main-light shadow sampling — implementation our own):
//   For each pixel: reconstruct the world ray, march up to the scene depth (capped at
//   _MaxDistance) in N jittered steps. Per sample the fog density is HEIGHT-based
//   (exponential falloff above _BaseHeight) and stirred by animated 3D noise (drifts with
//   the world wind global). Accumulate transmittance + in-scatter; the main light adds
//   anisotropic forward scattering, attenuated by its realtime SHADOW at each step — that is
//   what carves visible light shafts through geometry.
//   Corruption: fog colour stains pure → corrupt from the same `_WorldCorruption` global as
//   everything else (WorldAmbienceDriver owns it).
// Runs as a GameCameraFullScreenFeature pass (Game cameras only — scene-view depth is
// unreliable for depth-reconstruction passes in this project, see GameCameraFullScreenFeature).
// Injection: After Opaques + Sky, fetch colour. Full-res single pass; if it ever costs too
// much, the downsample+composite split is the known optimization path.
Shader "PoT/VolumetricFog"
{
    Properties
    {
        [Header(Fog Body)]
        _FogColor      ("Fog Colour (ambient in-scatter)", Color) = (0.72, 0.78, 0.88, 1)
        _FogColorCorr  ("Fog Colour when corrupted", Color) = (0.30, 0.16, 0.28, 1)
        _Density       ("Density", Range(0, 0.3)) = 0.05
        _BaseHeight    ("Base Height (world Y where fog is thickest)", Float) = 0
        _HeightFalloff ("Height Falloff (per metre above base)", Range(0.01, 2)) = 0.18
        _StartDistance ("Start Distance (m, keeps gameplay range clear)", Range(0, 60)) = 8
        _MaxDistance   ("March Distance Cap (m)", Range(20, 400)) = 140

        [Header(Noise (alive fog))]
        _NoiseScale    ("Noise Scale", Range(0.005, 0.5)) = 0.045
        _NoiseAmount   ("Noise Amount (0 = uniform fog)", Range(0, 1)) = 0.55
        _NoiseSpeed    ("Noise Drift Speed", Range(0, 3)) = 0.5

        [Header(Main Light Scattering)]
        _LightStrength ("Sun In-Scatter Strength", Range(0, 4)) = 1.2
        _Anisotropy    ("Anisotropy (glow toward the sun)", Range(0, 0.95)) = 0.6
        _ShadowStrength("Shadowing (carves the light shafts)", Range(0, 1)) = 1

        [Header(Quality)]
        [IntRange] _Steps ("Raymarch Steps", Range(8, 48)) = 24
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "PoTVolumetricFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor, _FogColorCorr;
                float _Density, _BaseHeight, _HeightFalloff, _StartDistance, _MaxDistance;
                float _NoiseScale, _NoiseAmount, _NoiseSpeed;
                float _LightStrength, _Anisotropy, _ShadowStrength;
                float _Steps;
            CBUFFER_END

            // globals — WorldAmbienceDriver / WindDriver
            float  _WorldCorruption;
            float4 _PoTWind;

            // 3D value noise (same family as the Coexistence shaders)
            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float vnoise3(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i);                     float n100 = hash13(i + float3(1,0,0));
                float n010 = hash13(i + float3(0,1,0));     float n110 = hash13(i + float3(1,1,0));
                float n001 = hash13(i + float3(0,0,1));     float n101 = hash13(i + float3(1,0,1));
                float n011 = hash13(i + float3(0,1,1));     float n111 = hash13(i + float3(1,1,1));
                return lerp(lerp(lerp(n000,n100,f.x), lerp(n010,n110,f.x), f.y),
                            lerp(lerp(n001,n101,f.x), lerp(n011,n111,f.x), f.y), f.z);
            }

            // Jimenez interleaved gradient noise — screen-space jitter that hides step banding.
            float IGN(float2 px)
            {
                return frac(52.9829189 * frac(0.06711056 * px.x + 0.00583715 * px.y + _Time.y * 0.6180339887));
            }

            // Henyey-Greenstein phase — forward scattering glow toward the sun.
            float HG(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * 3.14159265 * pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5));
            }

            float FogDensity(float3 p, float t)
            {
                float h = exp(-max(p.y - _BaseHeight, 0.0) * _HeightFalloff);          // height falloff
                float n = vnoise3(p * _NoiseScale + float3(_PoTWind.x, 0.05, _PoTWind.z) * (t * _NoiseSpeed));
                return _Density * h * lerp(1.0, n * 1.6, _NoiseAmount);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // scene depth → world position of the surface this pixel sees
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 1e-6;
                #else
                    bool isSky = rawDepth >= 1.0 - 1e-6;
                #endif
                float3 camPos = GetCameraPositionWS();
                float3 surfWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 ray = surfWS - camPos;
                float sceneDist = length(ray);
                float3 dir = ray / max(sceneDist, 1e-4);
                if (isSky) sceneDist = _MaxDistance;                       // fog fills toward the horizon

                float marchEnd = min(sceneDist, _MaxDistance);
                if (marchEnd <= _StartDistance + 0.01) return scene;

                int   steps = (int)_Steps;
                float stepLen = (marchEnd - _StartDistance) / steps;
                // jittered start hides banding (screen-space IGN, animated)
                float t0 = _StartDistance + stepLen * IGN(input.positionCS.xy);

                Light sun = GetMainLight();
                float phase = HG(dot(sun.direction, -dir), _Anisotropy);
                half3 fogCol = lerp(_FogColor.rgb, _FogColorCorr.rgb, saturate(_WorldCorruption));

                float time = _Time.y;
                float transmittance = 1.0;
                half3 inscatter = 0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float3 p = camPos + dir * (t0 + stepLen * i);
                    float d = FogDensity(p, time);
                    if (d <= 0.0001) continue;

                    float extinction = d * stepLen;
                    float absorb = exp(-extinction);

                    // sun shafts: light reaching this point through the shadow map
                    float shadow = 1.0;
                    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                        shadow = lerp(1.0, MainLightRealtimeShadow(TransformWorldToShadowCoord(p)), _ShadowStrength);
                    #endif

                    half3 sample = fogCol + sun.color.rgb * (phase * _LightStrength * shadow);
                    inscatter += sample * (transmittance * (1.0 - absorb));
                    transmittance *= absorb;
                    if (transmittance < 0.01) break;
                }

                return half4(scene.rgb * transmittance + inscatter, scene.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
