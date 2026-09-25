// PoT/CoexistenceFog — two-tone clan distance fog as a fullscreen pass.
//
// Used by a URP Full Screen Pass Renderer Feature (injection: Before Post Processing, fetch color).
// Distance fog whose colour is the BASE fog colour tinted toward Vethara violet on one side of the
// world axis and Luminari gold on the other ("the two clans share the air"), staining toward the
// corruption tint as the global _WorldCorruption grows — the same stain rule as every other
// Coexistence shader. Skybox pixels are skipped (the sky shader owns its own haze).
Shader "PoT/CoexistenceFog"
{
    Properties
    {
        [Header(Fog Body)]
        _FogColor   ("Fog Base Color", Color) = (0.78, 0.82, 0.90, 1)
        _FogDensity ("Density", Range(0.0001, 0.15)) = 0.015
        _FogStart   ("Start Distance (m)", Range(0, 80)) = 12
        _FogMax     ("Max Fog Amount", Range(0, 1)) = 0.85

        [Header(Clan Two Tone)]
        _ClanA      ("Clan A (Vethara violet)", Color) = (0.45, 0.20, 0.95, 1)
        _ClanB      ("Clan B (Luminari gold)",  Color) = (1.0, 0.78, 0.28, 1)
        _ClanTint   ("Clan Tint Strength", Range(0, 1)) = 0.25
        _FogAxis    ("Clan Axis (world XZ — A side → B side)", Vector) = (1, 0, 0, 0)

        [Header(Corruption (global driven))]
        _CorruptionColor ("Corruption Tint", Color) = (0.30, 0.09, 0.27, 1)
        _CorrAmount      ("Max Corruption Stain", Range(0, 1)) = 0.8
        _CorruptionBias  ("Corruption Bias (preview/test)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CoexistenceFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _FogDensity, _FogStart, _FogMax;
                half4 _ClanA, _ClanB;
                float _ClanTint;
                float4 _FogAxis;
                half4 _CorruptionColor;
                float _CorrAmount, _CorruptionBias;
            CBUFFER_END

            float _WorldCorruption;   // global (WorldAmbienceDriver) — outside the CBUFFER

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                // Skybox: at the far plane there is nothing to fog — the sky shader owns its haze.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 1e-6) return scene;
                #else
                    if (rawDepth >= 1.0 - 1e-6) return scene;
                #endif

                float3 wpos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 toPix = wpos - _WorldSpaceCameraPos;
                float dist = length(toPix);

                float fog = 1.0 - exp(-max(0.0, dist - _FogStart) * _FogDensity);
                fog = min(fog, _FogMax);
                if (fog <= 0.001) return scene;

                // Two-tone: which side of the clan axis this pixel's view direction falls on.
                float2 vXZ = normalize(toPix.xz + 1e-5);
                float2 axis = normalize(_FogAxis.xz + 1e-5);
                float side = saturate(dot(vXZ, axis) * 0.75 + 0.5);   // soft A→B ramp across the axis
                half3 clan = lerp(_ClanA.rgb, _ClanB.rgb, side);
                half3 fogCol = lerp(_FogColor.rgb, clan, _ClanTint);

                // Corruption stain — same rule as surfaces/sky: multiply toward the tint.
                float w = saturate(_WorldCorruption + _CorruptionBias);
                fogCol = lerp(fogCol, fogCol * _CorruptionColor.rgb * 1.8 + _CorruptionColor.rgb * 0.10,
                              saturate(w * _CorrAmount));

                return half4(lerp(scene.rgb, fogCol, fog), scene.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
