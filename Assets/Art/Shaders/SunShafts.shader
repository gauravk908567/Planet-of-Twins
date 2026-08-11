// PoT/SunShafts — screen-space god rays from the Coexistence sky's sun.
//
// Classic radial-gather: for each pixel, march toward the sun's SCREEN position accumulating
// bright SKY pixels (depth == far only, so ground/props never streak) with per-step decay.
// The moving cloud layer crossing the sun modulates the shafts naturally — clouds occlude,
// gaps beam. SunShaftsDriver (Persistent) feeds _SunUV + _SunVisibility per frame and the
// corruption global dims the shafts as the world falls (the sun dies with it).
// Runs as a Full Screen Pass (After CoexistenceFog, Before Post Processing, fetch color).
Shader "PoT/SunShafts"
{
    Properties
    {
        _ShaftColor   ("Shaft Tint", Color) = (1, 0.95, 0.82, 1)
        _Intensity    ("Intensity", Range(0, 3)) = 0.9
        _Threshold    ("Sky Brightness Threshold", Range(0, 2)) = 0.55
        _ShaftLength  ("Shaft Length (screen fraction)", Range(0.1, 1)) = 0.85
        _Decay        ("Per-step Decay", Range(0.8, 0.99)) = 0.94
        _CorrDim      ("Corruption Dims Shafts", Range(0, 1)) = 0.9

        // Driven per frame by SunShaftsDriver — not hand-authored.
        [HideInInspector] _SunUV        ("Sun Screen UV", Vector) = (0.5, 0.8, 0, 0)
        [HideInInspector] _SunVisibility("Sun Visibility 0..1", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "SunShafts"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShaftColor;
                float _Intensity, _Threshold, _ShaftLength, _Decay, _CorrDim;
                float4 _SunUV;
                float _SunVisibility;
            CBUFFER_END

            float _WorldCorruption;   // global (WorldAmbienceDriver)

            #define SHAFT_SAMPLES 40

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float fade = _SunVisibility * (1.0 - saturate(_WorldCorruption) * _CorrDim);
                if (fade <= 0.001 || _Intensity <= 0.001) return scene;

                // March from this pixel toward the sun's screen position.
                float2 delta = (_SunUV.xy - uv) * (_ShaftLength / SHAFT_SAMPLES);
                float2 p = uv;
                float illum = 1.0;
                half3 acc = 0;
                [unroll(SHAFT_SAMPLES)]
                for (int i = 0; i < SHAFT_SAMPLES; i++)
                {
                    p += delta;
                    float d = SampleSceneDepth(saturate(p));
                    // SKY only (far plane) — geometry must occlude, not become a light source.
                    #if UNITY_REVERSED_Z
                        float skyMask = d <= 1e-6 ? 1.0 : 0.0;
                    #else
                        float skyMask = d >= 1.0 - 1e-6 ? 1.0 : 0.0;
                    #endif
                    half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(p)).rgb;
                    float lum = dot(c, half3(0.299, 0.587, 0.114));
                    acc += c * (skyMask * saturate(lum - _Threshold) * illum);
                    illum *= _Decay;
                }
                acc /= SHAFT_SAMPLES;

                return half4(scene.rgb + acc * _ShaftColor.rgb * (_Intensity * 4.0 * fade), scene.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
