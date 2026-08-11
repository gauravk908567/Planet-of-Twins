// PoT/UIAbilityIcon — ability-icon symbol shader (SETUPGUIDE §21 part A; game.md §17.5 spec).
// The SYMBOL layer of an ability slot. The timer ring stays a SEPARATE sibling Image using
// PoT/UIRingTimer — this shader only handles the icon sprite itself:
//   • _DualClan off → single clan tint (_ClanColorA); on → left half A (Lyra gold) /
//     right half B (Kai violet) for COMMON abilities (user spec: half-half clan colours).
//   • Cooldown: grey desaturated symbol; _Recharge01 0→1 creeps the COLOUR back over the
//     symbol from the CENTRE OUTWARD (user-locked semantics: grey unavailable + centre-out
//     colour creep). _RevealMode 0 = centre-out radial, 1 = bottom-up (fallback look).
//   • Ready flash: bright band sweeping top→bottom via _FlashT 0→1 (view-driven, UNSCALED
//     time — Overwatch ability-ready). Dual-clan flash tints each half with its own colour.
//   • _DormantStrength darkens/desaturates the whole icon (Weaver's Gate / shared-emblem
//     dormant-until-available language) — independent of cooldown.
// ACES rule (SETUPGUIDE §20): clan fill colours stay ≤ ~1.3 intensity; only the flash goes hot.
Shader "PoT/UIAbilityIcon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Symbol Sprite", 2D) = "white" {}
        _Color ("Tint (Image.color multiplies)", Color) = (1,1,1,1)

        [Header(UI Internal)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Clan)]
        [Toggle] _DualClan ("Dual Clan Halves", Float) = 0
        [HDR] _ClanColorA ("Clan Colour A (single or LEFT half)", Color) = (1.0,0.78,0.29,1)
        [HDR] _ClanColorB ("Clan Colour B (RIGHT half when dual)", Color) = (0.55,0.35,1.0,1)
        _ClanTint ("Clan Tint Amount", Range(0,1)) = 0.85

        [Header(Cooldown)]
        _Recharge01 ("Recharge 0..1 (view-driven)", Range(0,1)) = 1
        [Enum(CentreOut,0,BottomUp,1)] _RevealMode ("Colour Reveal Mode", Float) = 0
        _RevealSoft ("Reveal Edge Softness", Range(0.001,0.3)) = 0.06
        _GreyColor ("Unavailable Grey", Color) = (0.45,0.47,0.52,1)
        _GreyStrength ("Grey Strength", Range(0,1)) = 1

        [Header(Ready Flash)]
        _FlashT ("Flash Sweep 0..1 (view-driven)", Range(0,1)) = 0
        _FlashWidth ("Flash Band Width", Range(0.02,0.6)) = 0.22
        _FlashBoost ("Flash Brightness", Range(0,6)) = 2.2

        [Header(Dormant)]
        _DormantStrength ("Dormant 0..1 (view-driven)", Range(0,1)) = 0
        _DormantColor ("Dormant Sink Colour", Color) = (0.10,0.10,0.14,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off  Lighting Off  ZWrite Off  ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIAbilityIcon"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; float4 _ClipRect;
            float _DualClan, _ClanTint;
            fixed4 _ClanColorA, _ClanColorB;
            float _Recharge01, _RevealMode, _RevealSoft, _GreyStrength;
            fixed4 _GreyColor;
            float _FlashT, _FlashWidth, _FlashBoost;
            float _DormantStrength;
            fixed4 _DormantColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sym = tex2D(_MainTex, i.uv);

                // clan tint over the sprite (any symbol works — white/greyscale art tints purest)
                fixed4 clan = (_DualClan > 0.5 && i.uv.x > 0.5) ? _ClanColorB : _ClanColorA;
                fixed3 tinted = lerp(sym.rgb, sym.rgb * clan.rgb, _ClanTint);

                // grey unavailable state: luminance pushed toward _GreyColor
                float lum = dot(sym.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 grey = lerp(sym.rgb, _GreyColor.rgb * lum * 2.0, _GreyStrength);

                // colour creep — reveal mask grows with _Recharge01
                float coord;
                if (_RevealMode < 0.5)
                    coord = saturate(length(i.uv - 0.5) / 0.7071);   // centre-out (0.7071 = corner)
                else
                    coord = saturate(1.0 - i.uv.y);                  // bottom-up (coord 0 at bottom)
                // filled where coord < recharge; soft edge so the creep front glows slightly
                float reveal = smoothstep(coord, coord + _RevealSoft, saturate(_Recharge01));
                fixed3 rgb = lerp(grey, tinted, reveal);
                // creep front highlight — a faint bright line at the reveal edge
                float front = smoothstep(0.0, _RevealSoft, saturate(_Recharge01) - coord)
                            * (1.0 - smoothstep(_RevealSoft, _RevealSoft * 2.0, saturate(_Recharge01) - coord));
                rgb += clan.rgb * front * 0.35 * step(0.001, saturate(_Recharge01)) * step(saturate(_Recharge01), 0.999);

                // ready flash — top→bottom band, per-half clan tint when dual, hot HDR allowed
                if (_FlashT > 0.001 && _FlashT < 0.999)
                {
                    float band = 1.0 - smoothstep(0.0, _FlashWidth, abs((1.0 - i.uv.y) - _FlashT));
                    fixed3 flashTint = (_DualClan > 0.5) ? clan.rgb : fixed3(1,1,1);
                    rgb += flashTint * band * _FlashBoost * sym.a;
                }

                // dormant sink (Weaver's Gate / emblem): darken + desaturate the whole icon
                if (_DormantStrength > 0.001)
                {
                    float dlum = dot(rgb, fixed3(0.299, 0.587, 0.114));
                    fixed3 sunk = lerp(fixed3(dlum, dlum, dlum), _DormantColor.rgb, 0.5);
                    rgb = lerp(rgb, sunk, _DormantStrength);
                }

                fixed4 col = fixed4(rgb, sym.a);
                col *= i.color;

                #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
