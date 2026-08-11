// PoT/DetailFoliage — terrain DETAIL-MESH foliage shader (grass tufts, pebbles, small plants).
//
// Why it exists: terrain detail meshes ("Vertex Lit" render mode + GPU instancing) need an
// instanced shader, and the game's world rule says EVERYTHING carries the corruption film —
// stock detail shaders can't. This is the `_WorldCorruption` graft for painted foliage.
//
//   • GPU-instanced, two-sided, alpha-cutout.
//   • Wind: reads the SAME WindDriver globals as PoT/Coexistence (_PoTWind xyz=dir w=strength,
//     _PoTWindGust). Sway mask = mesh-local Y (base rooted, tip bends) — set _WindAmount 0 for
//     rigid details (pebbles).
//   • Corruption: the blood-moon multiplicative film (albedo * corr * 2), driven by the
//     _WorldCorruption global — identical maths to PoT/Coexistence & PoT/TerrainLit, so painted
//     grass stains in lockstep with the terrain under it.
//   • Lighting: main light (with shadow attenuation) half-Lambert + SH ambient. No shadow
//     casting (deliberate — detail density × shadow maps is the classic perf trap).
Shader "PoT/DetailFoliage"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _Cutoff    ("Alpha Cutoff", Range(0.05, 0.95)) = 0.45

        [Header(Wind)]
        _WindAmount   ("Sway Amount (metres at tip)", Range(0, 0.5)) = 0.08
        _WindFrequency("Sway Frequency", Range(0.1, 5)) = 1.6

        [Header(Corruption)]
        _CorruptionColor   ("Corruption Tint", Color) = (0.22, 0.05, 0.16, 1)
        _CorruptionStrength("Max Film Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Cutoff;
                float _WindAmount, _WindFrequency;
                half4 _CorruptionColor;
                float _CorruptionStrength;
            CBUFFER_END

            // WindDriver + WorldAmbienceDriver globals (outside the cbuffer, shared project-wide)
            float4 _PoTWind;        // xyz = dir (y 0), w = strength 0..1
            float  _PoTWindGust;    // extra gust 0..1
            float  _WorldCorruption;
            float  _PoTWetness;     // RainController global (0 dry .. 1 soaked)

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Wind sway: tip-weighted (local Y), phase from world position so tufts
                // desynchronize; strength+gust from the WindDriver globals. Scaled time.
                float mask = saturate(IN.positionOS.y * 2.0) * _WindAmount;
                if (mask > 0.0001)
                {
                    float strength = saturate(_PoTWind.w + _PoTWindGust);
                    float phase = dot(posWS.xz, float2(0.37, 0.53));
                    float sway = sin(_Time.y * _WindFrequency * 2.0 + phase)
                               + 0.5 * sin(_Time.y * _WindFrequency * 4.7 + phase * 1.7);
                    posWS += _PoTWind.xyz * (sway * mask * strength);
                }

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, half facing : VFACE) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(tex.a - _Cutoff);

                float3 n = normalize(IN.normalWS) * (facing >= 0 ? 1 : -1);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction) * 0.5 + 0.5);   // half-Lambert
                half3 lit = tex.rgb * (mainLight.color * mainLight.shadowAttenuation * ndl
                                       + SampleSH(n));

                // Blood-moon corruption film — same maths as Coexistence/TerrainLit
                half3 film = lit * _CorruptionColor.rgb * 2.0;
                lit = lerp(lit, film, saturate(_WorldCorruption) * _CorruptionStrength);

                // Rain wetness — grass darkens with the soaked ground (RainController global)
                lit *= lerp(1.0, 0.72, saturate(_PoTWetness));

                lit = MixFog(lit, IN.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Cutoff;
                float _WindAmount, _WindFrequency;
                half4 _CorruptionColor;
                float _CorruptionStrength;
            CBUFFER_END

            float4 _PoTWind;
            float  _PoTWindGust;

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vertDepth(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                // Depth must match the forward pass — replicate the sway
                float mask = saturate(IN.positionOS.y * 2.0) * _WindAmount;
                if (mask > 0.0001)
                {
                    float strength = saturate(_PoTWind.w + _PoTWindGust);
                    float phase = dot(posWS.xz, float2(0.37, 0.53));
                    float sway = sin(_Time.y * _WindFrequency * 2.0 + phase)
                               + 0.5 * sin(_Time.y * _WindFrequency * 4.7 + phase * 1.7);
                    posWS += _PoTWind.xyz * (sway * mask * strength);
                }
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 fragDepth(Varyings IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
