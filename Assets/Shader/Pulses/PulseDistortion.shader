Shader "PlanetOfTwins/PulseDistortion"
{
    // Screen-space refraction ring (the shockwave "ripple"). Samples the URP Opaque Texture
    // (_CameraOpaqueTexture, via Scene Color) and offsets it radially inside an expanding ring
    // band, so the world appears to warp as the wavefront passes. Meant to ride ON TOP of the
    // emissive pulse ring at the same radius. Needs "Opaque Texture" ON in the URP asset.
    // Requires something with detail BEHIND it to be visible (a flat untextured plane shows little).
    Properties
    {
        _Strength  ("Distortion Strength", Range(0, 0.12)) = 0.03
        _RingPos   ("Ring Center (0..1 of quad radius)", Range(0,1)) = 0.82
        _RingWidth ("Ring Band Width", Range(0.02, 0.5)) = 0.16
        _Chroma    ("Chromatic Split", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 screenPos:TEXCOORD1; float4 color:COLOR; };

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _RingPos;
                float _RingWidth;
                float _Chroma;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;
                OUT.screenPos   = v.positionNDC;   // xy/w = 0..1 screen UV
                OUT.uv    = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uvc = IN.uv - 0.5;
                float  d   = length(uvc) * 2.0;                 // 0 center -> 1 quad edge
                float2 dir = d > 1e-4 ? uvc / (d * 0.5) : float2(0,0);

                // expanding wavefront band around _RingPos
                float ring = 1.0 - saturate(abs(d - _RingPos) / _RingWidth);
                ring = ring * ring;                             // tighten the band

                float fade = IN.color.a;                        // particle alpha drives overall strength + fade-out
                float amt  = ring * _Strength * fade;

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 off = dir * amt;

                // chromatic split: sample R/G/B at slightly different offsets for an energy fringe
                float2 cs = off * _Chroma;
                half r = SampleSceneColor(screenUV + off + cs).r;
                half g = SampleSceneColor(screenUV + off).g;
                half b = SampleSceneColor(screenUV + off - cs).b;

                return half4(r, g, b, ring * fade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
