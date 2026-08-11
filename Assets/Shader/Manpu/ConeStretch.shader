Shader "PlanetOfTwins/ConeStretch"
{
    // Prototype: a sprite that stretches vertically from a bottom pivot (the "dot")
    // AND tapers its width by height, so it reads as a CONE/WEDGE from the pivot
    // instead of a rectangle. _ConeAngle controls the flare (0 = rectangle, 1 = pinched
    // to a point at the pivot). _Height is the vertical stretch (drive it over time for the whip).
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Height ("Vertical Stretch", Float) = 1
        _ConeAngle ("Cone Taper (0=rect, 1=point)", Range(0,1)) = 0.45
        _PivotY ("Pivot Y (0=bottom/dot .. 1=top)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Height;
                float _ConeAngle;
                float _PivotY;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 p = IN.positionOS.xyz;      // Unity Quad: x,y in [-0.5,0.5], uv in [0,1]

                // vertical stretch anchored at the pivot (bottom = the dot)
                float pivotObjY = lerp(-0.5, 0.5, _PivotY);
                p.y = pivotObjY + (p.y - pivotObjY) * _Height;

                // cone taper: full width at the top, narrowing toward the pivot
                float width = 1.0 - _ConeAngle * (1.0 - IN.uv.y);
                p.x *= width;

                OUT.positionHCS = TransformObjectToHClip(p);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
