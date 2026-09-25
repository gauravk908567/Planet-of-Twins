Shader "PoT/UISDFGlow"
{
    // PoT UI SDF Glow — a Canvas (UI) OUTWARD corona for a sprite-based element, driven off a baked
    // signed-distance field. Sits as its own Image layer BEHIND the thing it haloes (e.g. the accord
    // bar's fill+frame). The keycap/card get their corona from a maths SDF (rounded box); a sprite has
    // no distance info, so we bake one offline (UI_AccordBar_SDF.png: R = distance, 0.5 = edge, brighter
    // inside, a spread of transparent-padding pixels outside so the glow has room to bleed). This shader
    // reads that field and draws a glow that hugs the exact silhouette and radiates outward — the same
    // "solar corona" the keycap does, on an ornamental sprite.
    //
    // The SDF texture MUST import as sRGB OFF (linear) — the R channel is a distance, not a colour, so
    // gamma would warp it. Alpha is 1 everywhere (full-rect), so the quad renders and the shader decides
    // the visible glow per-pixel. Driven each frame: _Glow (0 = off → 1 = full) from the bar driver.
    Properties
    {
        [PerRendererData] _MainTex ("SDF (R = distance, 0.5 = edge)", 2D) = "white" {}
        _Color ("Tint (Image.color multiplies this)", Color) = (1,1,1,1)

        // Canvas mask stencil plumbing — do not hand-edit
        [Header(UI Internal)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Corona)]
        [HDR] _CoronaColor ("Corona Colour", Color) = (1.0, 0.86, 0.5, 1)
        _Glow ("Glow (0 = off, script-driven)", Range(0,1)) = 0
        // How far OUTWARD the glow reaches, in SDF units below the 0.5 edge (bounded by the baked spread).
        _EdgeWidth ("Corona Reach (SDF units)", Range(0.02,0.5)) = 0.22
        _PulseSpeed ("Pulse Speed", Range(0,20)) = 6
        // Keep a floor of glow so a lit-but-not-pulsing bar still haloes; the pulse rides on top.
        _PulseDepth ("Pulse Depth", Range(0,1)) = 0.4
        _Intensity ("Intensity Multiplier", Range(0.5,4)) = 1.7
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // Additive-ish premultiplied so overlapping glow reads as light, not a grey box.
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            fixed4 _CoronaColor;
            float _Glow;
            float _EdgeWidth;
            float _PulseSpeed;
            float _PulseDepth;
            float _Intensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float sdf = tex2D(_MainTex, IN.texcoord).r;   // 0.5 = edge, >0.5 inside, <0.5 outside

                // Bright at the edge (0.5), fading OUTWARD to 0 over _EdgeWidth, and killed just INSIDE the
                // edge — a pure outward rim halo. So the glow never over-brightens the bar interior and the
                // layer can sit on top of or behind the fill/frame without changing the interior.
                half corona = smoothstep(0.5 - _EdgeWidth, 0.5, sdf)
                            * (1.0 - smoothstep(0.5, 0.5 + 0.03, sdf));

                half pulse = (1.0 - _PulseDepth) + _PulseDepth * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                half amt = saturate(_Glow) * pulse * corona;

                half3 rgb = _CoronaColor.rgb * amt * _Intensity;
                half a = amt * _CoronaColor.a;

                fixed4 color = fixed4(rgb * IN.color.rgb, a * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
