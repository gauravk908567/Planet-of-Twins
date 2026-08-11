// PoT/LocalFogVolume — placeable walk-through fog VOLUME (raymarched; 2026-07-24 rework).
//
// The Ghost of Tsushima / Where Winds Meet "smoke layer the player passes through". Put this on a
// stretched default Cube (a MeshRenderer, NO collider) laid over a path, hollow, courtyard, deck, or
// crack mouth. It is the LOCAL, author-placed fog — a separate system from the two others, do not
// confuse them:
//   • Global god-ray fog  = CristianQiu Volumetric Light (VOLUME-driven "FogVolume" in Persistent) —
//                            carves sun/moon shafts through shadows. Not a material. Leave it alone.
//   • Global distance fog  = PoT/CoexistenceFog.
//   • THIS                 = a hand-placed pocket you scale to fit a region. MATERIAL-driven (every
//                            knob is on the material, tweakable live in Art/Materials) and it DRIFTS
//                            with the world wind (_PoTWind — the same global the grass and lanterns
//                            move to), so one wind change moves this fog too.
//
// 2026-07-24: converted from the old flat single-sample alpha sheet to a real RAYMARCHED box volume,
// so it has genuine depth (thicker where you look through more of it) and true 3D wind-driven noise
// instead of a panning 2D sheet. Technique:
//   Render BACK faces (Cull Front) so a pixel's box is shaded exactly once whether the camera is
//   outside OR inside the box. Per fragment: build the world view ray, intersect the cube's own
//   object-space AABB [-0.5, 0.5]^3 (slab test — handles any position/rotation/scale), clamp the far
//   end to the opaque scene depth (walls/props occlude the fog per-ray), then march entry→exit
//   accumulating density. Density = vertical fill profile × animated 3D value-noise, softened by the
//   four classic fades (edge / height-feather / depth-contact / camera). The noise SCROLLS along the
//   wind direction in 3D with a BOUNDED time offset (fmod, two opposed layers): an unbounded
//   _Time.y * speed offset is exactly what dithered/stippled the old fullscreen fog once the
//   coordinate grew large, so we never do that. No shadow sampling — light shafts are the global
//   fog's job; this is a soft wind-blown body.
//
// Mesh convention: default unit Cube (object space ±0.5), scaled in the scene.
// Typical use: scale (20, 3, 12), base at ground level, Density ~0.5. Full setup: SETUPGUIDE §5.
Shader "PoT/LocalFogVolume"
{
    Properties
    {
        [Header(Fog Body)]
        _FogColor    ("Fog Color", Color) = (0.62, 0.66, 0.78, 1)
        _Density     ("Density", Range(0, 4)) = 0.5

        [Header(Fill and Vertical Gradient)]
        [Enum(Uniform,0,Bottom,1,Top,2,Scattered,3)] _GradientMode ("Vertical Gradient", Float) = 1
        _Fill        ("Fill Amount (fraction of box height)", Range(0, 1)) = 0.5
        _HeightFade  ("Gradient Feather (softness of the fill edge)", Range(0.02, 1)) = 0.7

        [Header(Noise (alive wind driven fog))]
        _NoiseScale  ("Noise Scale (world m)", Range(0.005, 0.5)) = 0.045
        _NoiseAmount ("Noise Break-up", Range(0, 1)) = 0.65

        [Header(Wind Drift)]
        _DriftSpeed     ("Drift Speed (with _PoTWind)", Range(0, 3)) = 0.8
        _DriftSecondary ("Second Layer Speed Mult", Range(0, 2)) = 0.55
        _DriftPeriod    ("Drift Wrap Period (s, keeps precision safe)", Range(30, 600)) = 120

        [Header(Soft Fades)]
        _DepthFade   ("Depth Fade (m, soft where fog meets geometry)", Range(0.05, 8)) = 1.5
        _CameraFade  ("Camera Fade (m, soft as camera enters)", Range(0.1, 15)) = 4
        _EdgeFade    ("Edge Fade (fraction of box, hides the walls)", Range(0.01, 0.5)) = 0.25

        [Header(Main Light Tint)]
        _LightInfluence ("Sun/Moon In-Scatter (subtle; shafts are the global fog)", Range(0, 2)) = 0.15
        _Anisotropy     ("Anisotropy (glow toward the light)", Range(0, 0.95)) = 0.5

        [Header(Corruption (global driven))]
        _CorruptionColor ("Corruption Tint", Color) = (0.26, 0.07, 0.19, 1)
        _CorrAmount      ("Max Corruption Stain", Range(0, 1)) = 0.8
        _CorruptionBias  ("Corruption Bias (preview/test)", Range(0, 1)) = 0

        [Header(Quality)]
        [IntRange] _Steps ("Raymarch Steps", Range(8, 48)) = 24
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LocalFog"

            // Premultiplied volumetric composite: rgb is coverage-weighted in-scatter, alpha = 1 - T.
            // ZTest Always + Cull Front: shade the box once (back faces), never let the box's own
            // depth reject the pixel — per-ray occlusion is done in the shader against the opaque
            // depth texture, which is correct even when only PART of the fog is behind geometry.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density;
                float _GradientMode, _Fill, _HeightFade;
                float _NoiseScale, _NoiseAmount;
                float _DriftSpeed, _DriftSecondary, _DriftPeriod;
                float _DepthFade, _CameraFade, _EdgeFade;
                float _LightInfluence, _Anisotropy;
                half4 _CorruptionColor;
                float _CorrAmount, _CorruptionBias;
                float _Steps;
            CBUFFER_END

            // globals — WorldAmbienceDriver / WindDriver own these, outside the CBUFFER (zero = inert)
            float  _WorldCorruption;
            float4 _PoTWind;        // xz direction (normalized), w strength 0..1
            float  _PoTWindGust;

            // ── 3D value noise (same family as the Coexistence shaders).
            float PoTFogHash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float PoTFogNoise3(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = PoTFogHash13(i);                   float n100 = PoTFogHash13(i + float3(1,0,0));
                float n010 = PoTFogHash13(i + float3(0,1,0));   float n110 = PoTFogHash13(i + float3(1,1,0));
                float n001 = PoTFogHash13(i + float3(0,0,1));   float n101 = PoTFogHash13(i + float3(1,0,1));
                float n011 = PoTFogHash13(i + float3(0,1,1));   float n111 = PoTFogHash13(i + float3(1,1,1));
                return lerp(lerp(lerp(n000,n100,f.x), lerp(n010,n110,f.x), f.y),
                            lerp(lerp(n001,n101,f.x), lerp(n011,n111,f.x), f.y), f.z);
            }

            // Jimenez interleaved gradient noise — screen-space jitter that hides raymarch banding.
            // The _Time.y term lives inside frac(), so it is inherently bounded (no precision drift).
            float IGN(float2 px)
            {
                return frac(52.9829189 * frac(0.06711056 * px.x + 0.00583715 * px.y + _Time.y * 0.6180339887));
            }

            float HG(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * 3.14159265 * pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5));
            }

            // Slab ray/box test against the unit box [-0.5, 0.5]^3 (a default Cube in object space).
            bool RayBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 invD = 1.0 / rd;
                float3 t0 = (-0.5 - ro) * invD;
                float3 t1 = ( 0.5 - ro) * invD;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);
                return tFar >= max(tNear, 0.0);
            }

            float VerticalProfile(float y01)
            {
                // y01: 0 = box bottom, 1 = box top. _HeightFade = feather width of the fill edge.
                UNITY_BRANCH
                if (_GradientMode < 0.5) return 1.0;                                                   // Uniform
                UNITY_BRANCH
                if (_GradientMode < 1.5)                                                                // Bottom-heavy
                    return 1.0 - smoothstep(_Fill, _Fill + _HeightFade, y01);
                // Top-heavy (fills down from the ceiling)
                return smoothstep((1.0 - _Fill) - _HeightFade, (1.0 - _Fill), y01);
            }

            // Bounded, wind-driven 3D break-up noise sampled at a world position.
            float DriftNoise(float3 pWS)
            {
                float  spd   = (0.4 + _PoTWind.w + _PoTWindGust * 0.5) * _DriftSpeed;
                float  drift = fmod(_Time.y * spd, _DriftPeriod);   // wrapped → coord never blows up
                float2 wdir  = _PoTWind.xz;
                if (dot(wdir, wdir) < 1e-4) wdir = normalize(float2(1, 0.3));  // still breathes with no driver
                float3 flow1 = float3(wdir.x,  0.06, wdir.y) * (drift * 0.10);
                float3 flow2 = float3(-wdir.x, 0.03, -wdir.y) * (drift * 0.10 * _DriftSecondary);
                float n1 = PoTFogNoise3(pWS * _NoiseScale - flow1);
                float n2 = PoTFogNoise3(pWS * _NoiseScale * 2.7 + 13.7 - flow2);
                return saturate(n1 * 0.65 + n2 * 0.45);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 camWS = _WorldSpaceCameraPos;
                float3 rdWS  = IN.positionWS - camWS;
                rdWS = normalize(rdWS);

                // Ray in object space → test the unit box (handles any position/rotation/scale).
                float3 roOS = TransformWorldToObject(camWS);
                float3 rdOS = normalize(IN.positionOS - roOS);

                float tN, tF;
                if (!RayBox(roOS, rdOS, tN, tF))
                    return half4(0, 0, 0, 0);

                // Object-space entry/exit → WORLD distances along the unit ray.
                float3 entryWS = TransformObjectToWorld(roOS + rdOS * max(tN, 0.0));
                float3 exitWS  = TransformObjectToWorld(roOS + rdOS * tF);
                float  tEntry  = max(dot(entryWS - camWS, rdWS), 0.0);
                float  tExit   = dot(exitWS - camWS, rdWS);

                // Occlude the far end against opaque scene geometry (per-ray). Flag whether the end is
                // geometry (→ soft depth-contact fade) or just the box's far wall (→ no contact fade).
                float2 uv = GetNormalizedScreenSpaceUV(IN.positionCS);
                float  rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 1e-6;
                #else
                    bool isSky = rawDepth >= 1.0 - 1e-6;
                #endif
                float endIsGeom = 0.0;
                if (!isSky)
                {
                    float3 opaqueWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    float  tOpaque  = dot(opaqueWS - camWS, rdWS);
                    if (tOpaque < tExit) { tExit = tOpaque; endIsGeom = 1.0; }
                }

                float marchLen = tExit - tEntry;
                if (marchLen <= 1e-4)
                    return half4(0, 0, 0, 0);

                int   steps   = (int)_Steps;
                float stepLen = marchLen / steps;
                float t0      = tEntry + stepLen * IGN(IN.positionCS.xy);   // jitter hides banding

                // Corruption stain — same multiply-toward-tint rule as the other fog shaders.
                half3 baseCol = _FogColor.rgb;
                float w = saturate(_WorldCorruption + _CorruptionBias);
                baseCol = lerp(baseCol, baseCol * _CorruptionColor.rgb * 1.8 + _CorruptionColor.rgb * 0.10,
                               saturate(w * _CorrAmount));

                Light sun    = GetMainLight();
                float phase  = HG(dot(sun.direction, -rdWS), _Anisotropy);
                half3 sunAdd = sun.color.rgb * (phase * _LightInfluence);
                half3 stepCol = baseCol + sunAdd;

                float transmittance = 1.0;
                half3 inscatter     = 0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t   = t0 + stepLen * i;
                    float3 pWS = camWS + rdWS * t;
                    float3 pOS = TransformWorldToObject(pWS);

                    // vertical fill profile
                    float y01  = saturate(pOS.y + 0.5);
                    float prof = VerticalProfile(y01);

                    // wind-drifted break-up
                    float n  = DriftNoise(pWS);
                    float nf = lerp(1.0, n * 1.6, _NoiseAmount);
                    UNITY_BRANCH
                    if (_GradientMode > 2.5)                       // Scattered — noise blobs, coverage from _Fill
                    {
                        float thr = lerp(0.72, 0.12, _Fill);
                        prof = smoothstep(thr, thr + 0.28, n);
                        nf   = 1.0;
                    }

                    // XZ edge fade (hide the box walls)
                    float2 fromEdge = 0.5 - abs(pOS.xz);
                    float edge = saturate(min(fromEdge.x, fromEdge.y) / max(_EdgeFade, 1e-4));

                    // camera fade (soft as the near samples approach the camera)
                    float camFade = saturate(t / _CameraFade);

                    // depth-contact fade (soft where the fog meets opaque geometry at the far end)
                    float contact = lerp(1.0, saturate((tExit - t) / _DepthFade), endIsGeom);

                    float d = _Density * prof * nf * edge * camFade * contact;
                    if (d <= 1e-4) continue;

                    float extinction = d * stepLen;
                    float absorb     = exp(-extinction);
                    inscatter     += stepCol * (transmittance * (1.0 - absorb));
                    transmittance *= absorb;
                    if (transmittance < 0.02) break;
                }

                return half4(inscatter, saturate(1.0 - transmittance));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
