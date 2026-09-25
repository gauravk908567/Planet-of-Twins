// PoT/UIRingTimerFX — rescue/QTE "mash & rundown" evolution of PoT/UIRingTimer (game.md §17.5).
// This is a DELIBERATE DUPLICATE of PoT/UIRingTimer (never edit the shared circle in place — it
// drives ability cooldowns + the real QTE in area scenes). It is a strict superset: the circle
// path is byte-identical, and it ADDS three feature-specific extras used only by the rescue/QTE
// button rings:
//   • _Shape   — 0 = circle (as the shared shader), 1 = rounded-rect / pill (Option B shared-QTE
//                capsule holding two glyphs side-by-side, filled left→right). Aspect-corrected so
//                the caps stay circular on a wide RectTransform (_Aspect = width/height).
//   • 2nd urgency stop (_UrgencyColor2 / _UrgencyThreshold2) — the TTK rundown heats
//                cyan → bright red (_UrgencyColor) → deep dark-red (_UrgencyColor2, still HDR-glowing).
//   • _MashPulse — a continuous centre-out attractor pulse (warm-white, non-clan) that makes an idle
//                mash prompt actively signal "mash me", on top of the per-press punch/ripple.
// Consumers: M_UIRingTimer_TTK (circle, 2-stage urgency), M_UIRingTimer_Button (circle, mash pulse),
// M_UIRingTimer_SharedQTE (rounded-rect capsule). Circle consumers behave exactly like the shared shader.
Shader "PoT/UIRingTimerFX"
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
        [Enum(Circle,0,RoundedRect,1)] _Shape ("Shape", Float) = 0
        _Aspect ("Aspect (width over height, rounded-rect)", Range(1,4)) = 2.2
        _CornerRadius ("Corner Radius (1 = full pill)", Range(0,1)) = 1
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
        [Toggle] _ClosingRing ("Closing Ring (QTE)", Float) = 0
        [HDR] _ClosingColor ("Closing Ring Colour", Color) = (1.5,1.45,1.3,0.9)
        _ClosingMax ("Closing Start Radius (x outer)", Range(1,2.5)) = 1.8
        _ClosingWidth ("Closing Ring Width", Range(0.005,0.08)) = 0.02
        _Ticks ("Tick Count", Range(0,24)) = 0
        _TickStrength ("Tick Strength", Range(0,1)) = 0.35
        _UrgencyThreshold ("Urgency Below Progress", Range(0,1)) = 0.25
        [HDR] _UrgencyColor ("Urgency Colour (bright red)", Color) = (1.8,0.55,0.35,1)
        _UrgencyPulse ("Urgency Pulse Speed", Range(0,20)) = 9

        [Header(FX Extras)]
        // Second urgency stop: below _UrgencyThreshold2 the fill heats PAST _UrgencyColor into
        // _UrgencyColor2 (deep dark-red) — the cyan -> bright red -> deep red TTK rundown.
        _UrgencyThreshold2 ("Deep Urgency Below Progress", Range(0,1)) = 0.10
        [HDR] _UrgencyColor2 ("Deep Urgency Colour (deep red)", Color) = (1.1,0.03,0.03,1)
        // Continuous centre-out "mash me" attractor pulse (idle prompt animation).
        [Toggle] _MashPulse ("Mash Pulse (centre-out)", Float) = 0
        [HDR] _MashPulseColor ("Mash Pulse Colour", Color) = (1.5,1.4,1.1,1)
        _MashPulseSpeed ("Mash Pulse Speed", Range(0,6)) = 1.6
        _MashPulseWidth ("Mash Pulse Width", Range(0.03,0.4)) = 0.16
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
            Name "UIRingTimerFX"
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
            float _Shape, _Aspect, _CornerRadius;
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
            float _UrgencyThreshold2;
            fixed4 _UrgencyColor2;
            float _MashPulse, _MashPulseSpeed, _MashPulseWidth;
            fixed4 _MashPulseColor;

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
                float prog = saturate(_Progress);

                // ---- geometry per shape ----
                float disc, rimBand, pulseR;
                float outerFull = 1.0;                       // full outer mask (circle innerGlow only)
                float closing = 0.0;
                bool isCircle = (_Shape < 0.5);

                if (isCircle)
                {
                    float outer = smoothstep(_OuterRadius, _OuterRadius - _EdgeSoft, r);
                    float inner = smoothstep(_InnerRadius, _InnerRadius + _EdgeSoft, r);
                    disc = outer * inner;
                    outerFull = outer;
                    if (_ClosingRing > 0.5)
                    {
                        float cr = lerp(_OuterRadius, _OuterRadius * _ClosingMax, prog);
                        closing = 1.0 - smoothstep(0.0, _ClosingWidth, abs(r - cr));
                    }
                    rimBand = smoothstep(_OuterRadius - _RimWidth - _EdgeSoft, _OuterRadius - _RimWidth, r) * outer;
                    pulseR = saturate(r / max(_OuterRadius, 1e-4));
                }
                else
                {
                    // rounded-rect / pill, aspect-corrected so caps stay circular on a wide quad
                    float2 pa = float2(p.x * _Aspect, p.y);
                    float2 hb = float2(0.5 * _Aspect, 0.5) - _EdgeSoft;
                    float cr = min(hb.x, hb.y) * _CornerRadius;
                    float2 d2 = abs(pa) - hb + cr;
                    float sdf = length(max(d2, 0.0)) + min(max(d2.x, d2.y), 0.0) - cr;   // <0 inside
                    disc = smoothstep(_EdgeSoft, -_EdgeSoft, sdf);
                    outerFull = disc;
                    rimBand = smoothstep(-_RimWidth - _EdgeSoft, -_RimWidth, sdf) * disc;  // outer shell
                    pulseR = saturate(length(pa / max(hb, 1e-4)));
                }
                if (disc <= 0.001 && closing <= 0.001 && _MashPulse < 0.5) discard;

                // fill coordinate per mode, remapped so "filled" = coord < progress
                float coord;
                if (_FillMode < 0.5)          // angular sweep, 0 at top, clockwise
                    coord = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                else if (_FillMode < 1.5)     // centre-outward
                    coord = pulseR;
                else if (_FillMode < 2.5)     // left → right
                    coord = saturate(i.uv.x);
                else                          // top → down
                    coord = saturate(1.0 - i.uv.y);
                if (_InvertFill > 0.5) coord = 1.0 - coord;

                float filled = step(coord, prog);

                // clan colour: single, or dual halves split on uv.x
                fixed4 clan = (_DualClan > 0.5 && i.uv.x > 0.5) ? _FillColorB : _FillColorA;

                // urgency (two-stage): below threshold the fill heats toward _UrgencyColor, and below
                // the deeper threshold heats further into _UrgencyColor2 — cyan -> red -> deep red.
                float pulse = 0.75 + 0.25 * sin(_Time.y * _UrgencyPulse);
                float urgency = 0.0;
                if (prog < _UrgencyThreshold && prog > 0.001)
                {
                    urgency = (1.0 - prog / max(_UrgencyThreshold, 1e-4)) * pulse;
                    clan.rgb = lerp(clan.rgb, _UrgencyColor.rgb, saturate(urgency) * _UrgencyColor.a);
                }
                if (prog < _UrgencyThreshold2 && prog > 0.001)
                {
                    float u2 = (1.0 - prog / max(_UrgencyThreshold2, 1e-4)) * pulse;
                    clan.rgb = lerp(clan.rgb, _UrgencyColor2.rgb, saturate(u2) * _UrgencyColor2.a);
                }

                fixed4 col = lerp(_BackColor, clan, filled);

                // leading tip glow — sweep mode only, sits at the moving edge
                if (_FillMode < 0.5 && prog > 0.001 && prog < 0.999)
                {
                    float tip = 1.0 - smoothstep(0.0, _TipWidth, abs(coord - prog));
                    col.rgb += _TipGlow.rgb * tip * _TipGlow.a;
                    col.a = max(col.a, tip * _TipGlow.a);
                }

                // rim — faint full backing ring + BRIGHT arc over the filled portion, soft inward glow
                float arc = rimBand * ((_FillMode < 0.5) ? filled : 1.0);
                col.rgb = lerp(col.rgb, _RimColor.rgb, rimBand * _RimColor.a * 0.2);
                col.a = max(col.a, rimBand * _RimColor.a * 0.2);
                col.rgb = lerp(col.rgb, _RimColor.rgb, arc * _RimColor.a);
                col.a = max(col.a, arc * _RimColor.a);
                if (isCircle)
                {
                    float innerGlow = smoothstep(_OuterRadius - _RimWidth * 4.0, _OuterRadius - _RimWidth, r)
                                      * outerFull * ((_FillMode < 0.5) ? filled : 1.0);
                    col.rgb += _RimColor.rgb * innerGlow * 0.25;
                }

                // segment ticks on the rim band (circle only, Ragnarok-style)
                if (isCircle && _Ticks > 0.5)
                {
                    float ang01 = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                    float seg = abs(frac(ang01 * _Ticks) - 0.5) * 2.0;
                    float tick = (1.0 - smoothstep(0.0, 0.08, seg)) * rimBand;
                    col.rgb *= 1.0 - tick * _TickStrength;
                }

                // continuous "mash me" pulse — a bidirectional WAVE: an outward ring born at the
                // centre that widens + travels PAST the ring band to the quad edge, plus a subtler
                // inward ring returning from the edge. Reads as a radiating pulse wave, not a flicker.
                float3 pulseRGB = 0.0;
                float pulseA = 0.0;
                if (_MashPulse > 0.5)
                {
                    float t = _Time.y * _MashPulseSpeed;
                    float rq = saturate(r / 0.5);                       // 0 centre .. 1 quad edge (beyond the ring)
                    float po = frac(t);                                // outward phase 0 -> 1
                    float wOut = _MashPulseWidth * (0.6 + 0.9 * po);   // band widens as it travels ("more")
                    float outw = (1.0 - smoothstep(0.0, wOut, abs(rq - po))) * saturate(1.2 - 0.6 * po);
                    float pin = frac(t + 0.5);                          // inward phase, offset half a cycle
                    float inw = (1.0 - smoothstep(0.0, _MashPulseWidth * 0.8, abs((1.0 - rq) - pin)))
                                * saturate(1.0 - pin) * 0.5;           // subtler return wave
                    float quadFade = 1.0 - smoothstep(0.86, 1.0, rq);  // soft fade at the quad rim
                    float g = (outw + inw) * quadFade;
                    pulseRGB = _MashPulseColor.rgb * g * _MashPulseColor.a;
                    pulseA   = g * _MashPulseColor.a * 0.55;
                }

                // closing ring draw (circle QTE only; urgency-tinted so the contraction heats up late)
                if (isCircle && closing > 0.001)
                {
                    fixed3 closeCol = lerp(_ClosingColor.rgb, _UrgencyColor.rgb, urgency);
                    col.rgb += closeCol * closing * _ClosingColor.a;
                    col.a = max(col.a, closing * _ClosingColor.a);
                }

                // ready flash — top→bottom band; per-half clan tint when dual
                if (_FlashT > 0.001 && _FlashT < 0.999)
                {
                    float bandF = 1.0 - smoothstep(0.0, _FlashWidth, abs((1.0 - i.uv.y) - _FlashT));
                    fixed3 flashTint = (_DualClan > 0.5) ? clan.rgb : fixed3(1,1,1);
                    col.rgb += flashTint * bandF * _FlashBoost;
                    col.a = max(col.a, bandF * saturate(_FlashBoost));
                }

                // composite: the ring (masked by its shape) with the mash pulse added as a glow that
                // can extend beyond the ring band across the quad. Straight-alpha over-operator, so a
                // non-mash material (pulseA = 0) reduces EXACTLY to the old `col.a *= mask` behaviour.
                float baseMask = max(disc, closing);
                float ringA = col.a * baseMask;
                float3 premRing = col.rgb * ringA;                 // premultiplied ring colour
                float outA = max(ringA, pulseA);
                if (outA <= 0.0009) discard;
                col = fixed4((premRing + pulseRGB) / max(outA, 1e-4), outA);
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
