// PoT/CoexistenceCrack — the world-scar crack glow, rebuilt (2026-07-16, replaces the old
// Shader Graphs/CrackGlow ZWrite-off additive that white-blew wherever the huge canyon walls
// stacked edge-on in view).
//
// Concept (user spec, kept from the original): the crack is a vertical energy canyon —
// DARK at the ground line, progressively LIGHTER/hotter toward the bottom of the wall.
// Additions:
//   • Corruption colour journey (Colour Bible §7): Pure-Current icy blue early →
//     Khal-Vor oily green late, driven by the SAME `_WorldCorruption` global as everything.
//   • Clan streaks: violet/gold flecks mixed along geometric edges (crease detection, same
//     signal as the house shader) — "the two clans bleed at the scar's edges".
//   • Slow vertical energy scroll (noise) so the glow feels alive.
// Rendering: OPAQUE + ZWrite On + DepthOnly pass — layered canyon geometry occludes itself
// like solid rock, so N overlapping walls can never sum to white. Cull Off (walls are seen
// from both sides inside the canyon). Emission is HDR but bounded (_Intensity ≤ 4).
Shader "PoT/CoexistenceCrack"
{
    Properties
    {
        [Header(Depth Gradient (ground is dark))]
        _DepthRange     ("Depth Range (m below origin to reach full glow)", Range(0.5, 40)) = 10
        _GradientPower  ("Gradient Power (higher = glow hugs the bottom)", Range(0.3, 6)) = 1.6

        [Header(Pure Current (corruption 0))]
        _TopColorPure   ("Top / Ground Colour", Color) = (0.05, 0.03, 0.12, 1)
        _BotColorPure   ("Bottom / Deep Colour (HDR)", Color) = (0.55, 0.25, 1.0, 1)

        [Header(Khal Vor (corruption 1))]
        _TopColorCorr   ("Top / Ground Colour", Color) = (0.02, 0.09, 0.07, 1)
        _BotColorCorr   ("Bottom / Deep Colour (HDR)", Color) = (0.14, 0.91, 0.62, 1)
        _CorruptionMax  ("Corruption Influence (0 = this crack never corrupts)", Range(0, 1)) = 1

        [Header(Glow)]
        _Intensity      ("Glow Intensity", Range(0.2, 4)) = 1.6
        _NoiseAmount    ("Glow Unevenness (current stirs the glow)", Range(0, 1)) = 0.45

        [Header(Current (the flowing energy))]
        _CurrentColor   ("Current Colour (HDR)", Color) = (0.85, 0.9, 1.3, 1)
        _CurrentColorCorr("Current Colour when corrupted (HDR)", Color) = (0.35, 1.1, 0.7, 1)
        _CurrentStrength("Current Strength (0 = no visible streams)", Range(0, 4)) = 1
        _CurrentSpeed   ("Current Speed", Range(0, 3)) = 0.25
        _CurrentScale   ("Current Scale", Range(0.05, 8)) = 0.6
        _CurrentThreshold("Current Coverage (higher = thinner streams)", Range(0, 1)) = 0.62
        _CurrentSoftness("Current Softness", Range(0.02, 0.5)) = 0.15
        [NoScaleOffset] _CurrentTex ("Current Noise Texture (optional)", 2D) = "gray" {}
        _CurrentTexInfluence ("Texture Influence (0 = procedural noise)", Range(0, 1)) = 0

        [Header(Clan Streaks on Edges)]
        _ClanA          ("Clan A (Vethara violet)", Color) = (0.45, 0.20, 0.95, 1)
        _ClanB          ("Clan B (Luminari gold)",  Color) = (1.0, 0.78, 0.28, 1)
        _StreakStrength ("Streak Strength", Range(0, 3)) = 0.8
        _StreakScale    ("Streak Band Scale", Range(0.1, 8)) = 2.0
        _CreaseStrength ("Edge Detect Strength", Range(0, 30)) = 8

        _CorruptionBias ("Corruption Bias (preview/test)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "CrackGlow"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColorPure, _BotColorPure, _TopColorCorr, _BotColorCorr;
                float4 _CurrentColor, _CurrentColorCorr;
                float4 _ClanA, _ClanB;
                float  _DepthRange, _GradientPower, _Intensity, _NoiseAmount;
                float  _CurrentStrength, _CurrentSpeed, _CurrentScale, _CurrentThreshold, _CurrentSoftness, _CurrentTexInfluence;
                float  _StreakStrength, _StreakScale, _CreaseStrength;
                float  _CorruptionBias, _CorruptionMax;
            CBUFFER_END

            float _WorldCorruption;   // global (WorldAmbienceDriver) — outside the CBUFFER

            TEXTURE2D(_CurrentTex); SAMPLER(sampler_CurrentTex);   // optional authored flow noise

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float vnoise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash12(i), hash12(i + float2(1,0)), f.x),
                            lerp(hash12(i + float2(0,1)), hash12(i + float2(1,1)), f.x), f.y);
            }

            V vert(A IN)
            {
                V OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(V IN) : SV_Target
            {
                // _CorruptionMax lets a material opt out of (or soften) the world's corruption
                float w = saturate(_WorldCorruption + _CorruptionBias) * _CorruptionMax;

                // depth below the object's origin (the crack sits at ground level) → 0 top, 1 deep
                float originY = UNITY_MATRIX_M._m13;
                float depth01 = saturate((originY - IN.positionWS.y) / _DepthRange);
                float g = pow(depth01, 1.0 / max(_GradientPower, 0.01));   // >1 pulls glow down

                // the flowing current: scrolled noise — procedural, or the authored texture
                float2 cuv = float2(IN.positionWS.x + IN.positionWS.z,
                                    IN.positionWS.y * 2.0 - _Time.y * _CurrentSpeed) * _CurrentScale;
                float n = vnoise2(cuv * 3.0);
                if (_CurrentTexInfluence > 0.001)
                {
                    float tex = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, cuv * 0.35).r;
                    n = lerp(n, tex, _CurrentTexInfluence);
                }
                g = saturate(g * (1.0 - _NoiseAmount * 0.5 + n * _NoiseAmount));   // current stirs the glow

                // corruption journey: icy Pure Current → oily Khal-Vor (Colour Bible §7)
                half3 top = lerp(_TopColorPure.rgb, _TopColorCorr.rgb, w);
                half3 bot = lerp(_BotColorPure.rgb, _BotColorCorr.rgb, w);
                half3 col = lerp(top, bot, g) * _Intensity;

                // visible current streams — their OWN colour layer on top of the glow
                float stream = smoothstep(_CurrentThreshold - _CurrentSoftness,
                                          _CurrentThreshold + _CurrentSoftness, n);
                half3 curCol = lerp(_CurrentColor.rgb, _CurrentColorCorr.rgb, w);
                col += curCol * stream * _CurrentStrength * (0.25 + g * 0.75);

                // clan streaks on geometric edges — vertical bands pick violet vs gold
                float crease = saturate(length(fwidth(normalize(IN.normalWS))) * _CreaseStrength);
                float band = vnoise2(float2(IN.positionWS.y * _StreakScale,
                                            (IN.positionWS.x + IN.positionWS.z) * _StreakScale * 0.4));
                half3 clan = lerp(_ClanA.rgb, _ClanB.rgb, step(0.5, band));
                col += clan * crease * _StreakStrength * (0.3 + g * 0.7);

                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V depthVert(A IN) { V OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half4 depthFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
