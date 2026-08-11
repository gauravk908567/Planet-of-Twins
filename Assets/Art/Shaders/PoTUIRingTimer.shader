// PoT/UIRingTimer — the ONE timer/cooldown widget language (game.md §17.5).
// Used by: ability icon cooldowns, QTE timer, any radial rundown. NOT hollow — the fill is a
// solid disc (set _InnerRadius > 0 only if a specific consumer wants a band).
//
// Spec it implements (user-dictated 2026-07-21):
//   • _Progress 0..1 fills in the icon's colour; _FillMode selects HOW it fills:
//       0 = angular sweep (clock, from top, clockwise)   1 = centre-outward growth
//       2 = left→right                                   3 = top→down
//     _InvertFill flips any mode's direction.
//   • _DualClan: single clan colour (off) or BOTH clan colours as half rings — left half
//     _FillColorA (gold/Lyra), right half _FillColorB (violet/Kai).
//   • Sweep mode gets a glowing leading tip (_TipGlow) — the "clear rundown" read.
//   • Ready flash: a bright band sweeping top→bottom, driven by _FlashT 0→1 (UIRingTimerView
//     animates it once on recharge-complete; Overwatch ability-ready reference). Dual-clan
//     flash tints each half with its own clan colour.
Shader "PoT/UIRingTimer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Symbol (optional, drawn on top)", 2D) = "white" {}
        _Color ("Tint (Image.color multiplies)", Color) = (1,1,1,1)

        [Header(UI Internal)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Shape)]
        _OuterRadius ("Outer Radius", Range(0.1,0.5)) = 0.46
        _InnerRadius ("Inner Radius (0 = solid disc)", Range(0,0.49)) = 0.0
        _EdgeSoft ("Edge Antialias", Range(0.001,0.05)) = 0.01
        _RimWidth ("Rim Ring Width", Range(0,0.15)) = 0.035
        [HDR] _RimColor ("Rim Ring Colour", Color) = (0.85,0.88,1.05,0.9)

        [Header(Fill)]
        _Progress ("Progress 0..1", Range(0,1)) = 0.65
        [Enum(Sweep,0,CentreOut,1,LeftRight,2,TopDown,3)] _FillMode ("Fill Mode", Float) = 0
        [Toggle] _InvertFill ("Invert Direction", Float) = 0
        [Toggle] _DualClan ("Dual Clan Halves", Float) = 0
        [HDR] _FillColorA ("Fill Colour A (single or LEFT half)", Color) = (1.0,0.78,0.29,0.85)
        [HDR] _FillColorB ("Fill Colour B (RIGHT half when dual)", Color) = (0.55,0.35,1.0,0.85)
        _BackColor ("Unfilled Colour", Color) = (0.05,0.055,0.08,0.55)
        [HDR] _TipGlow ("Leading Tip Glow (sweep mode)", Color) = (1.4,1.4,1.5,1)
        _TipWidth ("Tip Width", Range(0.001,0.2)) = 0.03

        [Header(Ready Flash)]
        _FlashT ("Flash Sweep 0..1 (view-driven)", Range(0,1)) = 0
        _FlashWidth ("Flash Band Width", Range(0.02,0.6)) = 0.22
        _FlashBoost ("Flash Brightness", Range(0,6)) = 2.2

        [Header(QTE Extras)]
        // Sekiro-style closing ring: a ghost ring contracts from outside onto the rim as
        // progress runs 1 -> 0. The single most readable "act NOW" telegraph.
        [Toggle] _ClosingRing ("Closing Ring (QTE)", Float) = 0
        [HDR] _ClosingColor ("Closing Ring Colour", Color) = (1.5,1.45,1.3,0.9)
        _ClosingMax ("Closing Start Radius (x outer)", Range(1,2.5)) = 1.8
        _ClosingWidth ("Closing Ring Width", Range(0.005,0.08)) = 0.02
        // Thin segment ticks around the rim (Ragnarok-style clean segmentation). 0 = off.
        _Ticks ("Tick Count", Range(0,24)) = 0
        _TickStrength ("Tick Strength", Range(0,1)) = 0.35
        // Final-stretch urgency: below the threshold the fill/rim lerp to the urgency colour
        // and pulse. Reads "running out" without needing to track the arc consciously.
        _UrgencyThreshold ("Urgency Below Progress", Range(0,1)) = 0.25
        [HDR] _UrgencyColor ("Urgency Colour", Color) = (1.8,0.55,0.35,1)
        _UrgencyPulse ("Urgency Pulse Speed", Range(0,20)) = 9
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
            Name "UIRingTimer"
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
            float _OuterRadius, _InnerRadius, _EdgeSoft, _RimWidth;
            fixed4 _RimColor;
            float _Progress, _FillMode, _InvertFill, _DualClan;
            fixed4 _FillColorA, _FillColorB, _BackColor, _TipGlow;
            float _TipWidth;
            float _FlashT, _FlashWidth, _FlashBoost;
            float _ClosingRing, _ClosingMax, _ClosingWidth;
            fixed4 _ClosingColor;
            float _Ticks, _TickStrength;
            float _UrgencyThreshold, _UrgencyPulse;
            fixed4 _UrgencyColor;

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
                float2 p = i.uv - 0.5;                       // centred UV
                float r = length(p);

                // disc mask (solid unless _InnerRadius raised)
                float outer = smoothstep(_OuterRadius, _OuterRadius - _EdgeSoft, r);
                float inner = smoothstep(_InnerRadius, _InnerRadius + _EdgeSoft, r);
                float disc = outer * inner;

                // closing ring lives OUTSIDE the disc — its radius contracts from
                // _ClosingMax*outer down to the rim as progress runs 1 -> 0
                float closing = 0.0;
                if (_ClosingRing > 0.5)
                {
                    float cr = lerp(_OuterRadius, _OuterRadius * _ClosingMax, saturate(_Progress));
                    closing = 1.0 - smoothstep(0.0, _ClosingWidth, abs(r - cr));
                }
                if (disc <= 0.001 && closing <= 0.001) discard;

                // fill coordinate per mode, remapped so "filled" = coord < progress
                float prog = saturate(_Progress);
                float coord;
                if (_FillMode < 0.5)          // angular sweep, 0 at top, clockwise
                {
                    coord = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                }
                else if (_FillMode < 1.5)     // centre-outward
                {
                    coord = saturate(r / max(_OuterRadius, 1e-4));
                }
                else if (_FillMode < 2.5)     // left → right
                {
                    coord = saturate(i.uv.x);
                }
                else                          // top → down
                {
                    coord = saturate(1.0 - i.uv.y);
                }
                if (_InvertFill > 0.5) coord = 1.0 - coord;

                float filled = step(coord, prog);

                // clan colour: single, or dual halves split on uv.x
                fixed4 clan = (_DualClan > 0.5 && i.uv.x > 0.5) ? _FillColorB : _FillColorA;

                // urgency: below the threshold the fill heats toward _UrgencyColor and pulses
                float urgency = 0.0;
                if (prog < _UrgencyThreshold && prog > 0.001)
                {
                    float pulse = 0.75 + 0.25 * sin(_Time.y * _UrgencyPulse);
                    urgency = (1.0 - prog / max(_UrgencyThreshold, 1e-4)) * pulse;
                    clan.rgb = lerp(clan.rgb, _UrgencyColor.rgb, urgency * _UrgencyColor.a);
                }

                fixed4 col = lerp(_BackColor, clan, filled);

                // leading tip glow — sweep mode only, sits at the moving edge
                if (_FillMode < 0.5 && prog > 0.001 && prog < 0.999)
                {
                    float tip = 1.0 - smoothstep(0.0, _TipWidth, abs(coord - prog));
                    col.rgb += _TipGlow.rgb * tip * _TipGlow.a;
                    col.a = max(col.a, tip * _TipGlow.a);
                }

                // rim — Overwatch-resurrect style: a faint full-circle backing ring, with the
                // BRIGHT arc only over the filled portion (the arc grows with progress and the
                // tip glow rides its end), plus a soft glow bleeding inward from the arc.
                float rimBand = smoothstep(_OuterRadius - _RimWidth - _EdgeSoft, _OuterRadius - _RimWidth, r) * outer;
                float arc = rimBand * ((_FillMode < 0.5) ? filled : 1.0);
                // backing ring at 20% so the full circle always reads
                col.rgb = lerp(col.rgb, _RimColor.rgb, rimBand * _RimColor.a * 0.2);
                col.a = max(col.a, rimBand * _RimColor.a * 0.2);
                // bright arc + inward glow
                col.rgb = lerp(col.rgb, _RimColor.rgb, arc * _RimColor.a);
                col.a = max(col.a, arc * _RimColor.a);
                float innerGlow = smoothstep(_OuterRadius - _RimWidth * 4.0, _OuterRadius - _RimWidth, r)
                                  * outer * ((_FillMode < 0.5) ? filled : 1.0);
                col.rgb += _RimColor.rgb * innerGlow * 0.25;

                // segment ticks on the rim band (thin dark marks, Ragnarok-style)
                if (_Ticks > 0.5)
                {
                    float ang01 = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                    float seg = abs(frac(ang01 * _Ticks) - 0.5) * 2.0;   // 1 at segment centre, 0 at boundary
                    float tick = (1.0 - smoothstep(0.0, 0.08, seg)) * rimBand;
                    col.rgb *= 1.0 - tick * _TickStrength;
                }

                // closing ring draw (urgency-tinted so the contraction also heats up late)
                if (closing > 0.001)
                {
                    fixed3 closeCol = lerp(_ClosingColor.rgb, _UrgencyColor.rgb, urgency);
                    col.rgb += closeCol * closing * _ClosingColor.a;
                    col.a = max(col.a, closing * _ClosingColor.a);
                }

                // ready flash — top→bottom band; per-half clan tint when dual
                if (_FlashT > 0.001 && _FlashT < 0.999)
                {
                    float band = 1.0 - smoothstep(0.0, _FlashWidth, abs((1.0 - i.uv.y) - _FlashT));
                    fixed3 flashTint = (_DualClan > 0.5) ? clan.rgb : fixed3(1,1,1);
                    col.rgb += flashTint * band * _FlashBoost;
                    col.a = max(col.a, band * saturate(_FlashBoost));
                }

                // optional symbol texture on top, then UI plumbing
                fixed4 sym = tex2D(_MainTex, i.uv);
                col.rgb = lerp(col.rgb, sym.rgb, sym.a * 0.0); // symbol slot reserved; view composites icons separately
                col *= i.color;
                col.a *= max(disc, closing);   // closing ring lives outside the disc mask

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
