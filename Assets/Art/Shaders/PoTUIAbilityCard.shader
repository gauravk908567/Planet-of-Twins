// PoT/UIAbilityCard — the couch ability-HUD "border-as-timer" card (Track D / Option B).
// A rounded-rect UI card that carries clan IDENTITY and the ability TIMER in one surface:
//   • BORDER  = rounded-rect rim stroke with a travelling RUNNER (leading tip). Driven by
//               _BorderProgress 0..1. Single clan colour, or DUAL (gold LEFT / violet RIGHT)
//               for shared abilities. _BorderInvert flips the sweep direction.
//   • TANK    = bottom-anchored LIQUID fill inside the card (the interior glow). Height =
//               _TankProgress 0..1; direction dial _TankDir (bottom-up / top-down / L→R / R→L),
//               like the Coexistence directional fill. Cooldown RAISES it 0→1; active DRAINS 1→0.
//   • GLOW    = _GlowIntensity scales the emission (available 0.7 / active 1.0 / cooldown dim).
//   • GLYPH   = optional _IconTex ability/clan symbol composited in the centre.
//   • SHINE   = periodic highlight sweep while available (_ShineOn).
//   • HOLD    = _HoldProgress / _Pulse reserved for charge abilities (Empower) — implosion→pulse.
// UI plumbing (stencil / clip-rect / alpha-clip / vertex colour) mirrors PoT/UIRingTimer.
// The BorderFillDriver clones this material per slot (UI ignores MaterialPropertyBlock) and pushes
// the live 0..1 values every frame; the static "look" dials live on the material.
Shader "PoT/UIAbilityCard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (UI, unused)", 2D) = "white" {}
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
        _Aspect ("Aspect (width / height, driver-set)", Float) = 1.0
        _Margin ("Inset from edge", Range(0,0.25)) = 0.03
        _Corner ("Corner Radius", Range(0,0.5)) = 0.12
        _EdgeSoft ("Edge Antialias", Range(0.001,0.05)) = 0.006
        _BaseColor ("Card Body (faint, never flat clan tint)", Color) = (0.04,0.045,0.06,0.35)
        _InteriorWash ("Interior Clan Wash (light gradient behind glyph)", Range(0,1)) = 0.2

        [Header(Clan)]
        [HDR] _ClanA ("Clan A (single, or LEFT half)", Color) = (1.5,1.12,0.42,1)   // EMISSIVE Lyra / Luminari gold
        [HDR] _ClanB ("Clan B (RIGHT half when dual)", Color) = (1.0,0.66,1.5,1)     // EMISSIVE Kai / Vethara violet
        [Toggle] _DualClan ("Dual Clan (shared)", Float) = 0
        [HDR] _CasterClan ("Caster clan (single-caster override)", Color) = (1.5,1.12,0.42,1)
        [Toggle] _UseCaster ("Use Caster Clan (glow/flash/hold/pulse)", Float) = 0

        [Header(Border ring)]
        [Toggle] _BorderOn ("Border On", Float) = 1
        _BorderWidth ("Border Width", Range(0.005,0.12)) = 0.03
        _BorderProgress ("Border Progress 0..1", Range(0,1)) = 1
        [Toggle] _BorderInvert ("Invert Sweep", Float) = 0
        _BorderBackAlpha ("Unfilled Border Faint", Range(0,1)) = 0.18
        _RunnerWidth ("Runner Width", Range(0.005,0.25)) = 0.05
        [HDR] _RunnerGlow ("Runner Glow", Color) = (1.5,1.5,1.6,1)

        [Header(Tank liquid fill)]
        [Toggle] _TankOn ("Tank On", Float) = 1
        _TankProgress ("Tank Progress 0..1", Range(0,1)) = 0.5
        [Enum(BottomUp,0,TopDown,1,LeftRight,2,RightLeft,3)] _TankDir ("Tank Direction", Float) = 0
        _TankSoft ("Tank Surface Soften", Range(0.001,0.1)) = 0.02
        _GlowIntensity ("Glow Intensity (state)", Range(0,2)) = 0.7
        _SurfaceWidth ("Liquid Surface Line", Range(0,0.15)) = 0.03
        [HDR] _SurfaceGlow ("Liquid Surface Glow", Color) = (1.3,1.3,1.4,1)
        _FillBrightness ("Interior Fill Brightness (BG tint behind glyph — keeps the button legible)", Range(0,1)) = 0.45
        _KeycapCutout ("Keycap cutout (keep tank fill out of the centre where the keycap sits)", Range(0,1)) = 0

        [Header(Symbol)]
        _IconTex ("Ability Symbol", 2D) = "black" {}
        [HDR] _IconColor ("Symbol Tint", Color) = (1,1,1,1)
        _IconScale ("Symbol Scale", Range(0.2,1)) = 0.6

        [Header(Shine sweep)]
        [Toggle] _ShineOn ("Shine On (available)", Float) = 0
        [HDR] _ShineColor ("Shine Colour", Color) = (1.2,1.2,1.3,1)
        _ShineInterval ("Shine Interval (s)", Range(0.5,10)) = 4
        _ShineSpeed ("Shine Sweep Speed", Range(0.5,8)) = 3
        _ShineWidth ("Shine Band Width", Range(0.02,0.5)) = 0.12

        [Header(Flash (feedback pulse))]
        _Flash ("Flash 0..1 (driver-pulsed on soul gain etc.)", Range(0,1)) = 0

        [Header(Hold charge (reserved))]
        _HoldProgress ("Hold Progress 0..1", Range(0,1)) = 0
        _Pulse ("Release Pulse 0..1", Range(0,1)) = 0

        [Header(Active corona (solar eclipse))]
        _ActiveGlow ("Active corona 0..1 (driver-set while firing)", Range(0,1)) = 0
        _ActiveGlowWidth ("Active corona reach (outward, height-units)", Range(0.02,0.3)) = 0.13
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
            Name "UIAbilityCard"
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
            sampler2D _IconTex;
            fixed4 _Color; float4 _ClipRect;
            float _Aspect, _Margin, _Corner, _EdgeSoft;
            fixed4 _BaseColor, _ClanA, _ClanB, _CasterClan;
            float _DualClan, _UseCaster, _InteriorWash;
            float _BorderOn, _BorderWidth, _BorderProgress, _BorderInvert, _BorderBackAlpha, _RunnerWidth;
            fixed4 _RunnerGlow;
            float _TankOn, _TankProgress, _TankDir, _TankSoft, _GlowIntensity, _SurfaceWidth, _FillBrightness, _KeycapCutout;
            fixed4 _SurfaceGlow;
            fixed4 _IconColor; float _IconScale;
            float _ShineOn; fixed4 _ShineColor; float _ShineInterval, _ShineSpeed, _ShineWidth;
            float _HoldProgress, _Pulse, _Flash, _ActiveGlow, _ActiveGlowWidth;

            // Rounded-box signed distance (negative inside).
            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

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
                // Centre coord in "height units": x spans +/-0.5*aspect, y spans +/-0.5.
                float asp = max(_Aspect, 0.0001);
                float2 p = (i.uv - 0.5);
                p.x *= asp;

                float2 ext = float2(0.5 * asp, 0.5) - _Margin;
                float d = sdRoundBox(p, ext, _Corner);   // <0 inside the card

                // Active corona (solar eclipse): a clan glow radiating OUTWARD beyond the cell edge while firing —
                // brightest at the edge (d=0), fading out over _ActiveGlowWidth; a thin inner touch seats it on the
                // edge. Computed BEFORE the discard so the outward halo survives where there is no card body.
                float _aPulse = 0.6 + 0.4 * sin(_Time.y * 7.5);
                float _aAmt   = _ActiveGlow * _aPulse;
                float coronaGlow = 0.0;
                if (_aAmt > 0.001)
                {
                    float outward = (d > 0.0) ? smoothstep(_ActiveGlowWidth, 0.0, d)   // outside: 1 at edge → 0 at reach
                                              : smoothstep(-0.05, 0.0, d);              // inside: thin touch at the edge
                    coronaGlow = outward * _aAmt;
                }

                // Masks
                float body     = smoothstep(_EdgeSoft, -_EdgeSoft, d);                       // whole card
                float interior = smoothstep(-_BorderWidth, -_BorderWidth - _EdgeSoft, d);    // inside the border
                float rim      = 1.0 - smoothstep(_BorderWidth * 0.5 - _EdgeSoft,
                                                  _BorderWidth * 0.5 + _EdgeSoft,
                                                  abs(d + _BorderWidth * 0.5));               // border stroke band
                if (body <= 0.001 && coronaGlow <= 0.001) discard;

                // Clan colour: single, or dual halves split on card centre.
                fixed4 clan = (_DualClan > 0.5 && i.uv.x > 0.5) ? _ClanB : _ClanA;
                // Joint single-caster override (Empower): glow/flash/hold/pulse take the caster's clan; border stays split.
                fixed3 effClan = (_UseCaster > 0.5) ? _CasterClan.rgb : clan.rgb;

                // Interior fill/wash uses a TONE-MAPPED, dimmed clan so the body BEHIND the glyph stays a gentle
                // tint, not a hot flood. saturate() clamps the HDR over-1 clip (emissive violet's B=1.5 was blooming
                // harsh pink); _FillBrightness dims it so the keycap stays legible even when the fill covers it. The
                // BORDER/runner keep the full emissive clan (the requested glow) — only the BG calms down.
                fixed3 fillClan = saturate(effClan) * _FillBrightness;

                // Start from the faint card body, then add a VERY LIGHT clan gradient wash over the interior — the
                // soft "keycap surface" behind the glyph (user 2026-09-06: "rest of the space a very light gradient
                // of colour that enhances the button's visibility"). Lighter toward the top.
                fixed4 col = _BaseColor * body;
                float wgrad = lerp(0.4, 1.0, i.uv.y);
                col.rgb += fillClan * _InteriorWash * wgrad * interior;
                col.a = max(col.a, _InteriorWash * wgrad * interior * 0.7);

                // ── TANK (bottom-anchored liquid, directional) ──────────────────
                if (_TankOn > 0.5)
                {
                    float fc;
                    if (_TankDir < 0.5)      fc = i.uv.y;          // BottomUp  (lit where uv.y < prog)
                    else if (_TankDir < 1.5) fc = 1.0 - i.uv.y;    // TopDown
                    else if (_TankDir < 2.5) fc = i.uv.x;          // LeftRight
                    else                     fc = 1.0 - i.uv.x;    // RightLeft

                    // Keycap cutout: keep the bright liquid OUT of the central disc where the keycap/glyph sits — the
                    // cap is semi-transparent, so a full tank behind it washed the glyph out. 0 = no cutout (default).
                    float cdistTank = length(p) / max(length(ext), 0.0001);   // 0 centre → ~1 corner
                    float keepTank = (_KeycapCutout > 0.001)
                                   ? smoothstep(_KeycapCutout * 0.72, _KeycapCutout, cdistTank)
                                   : 1.0;

                    float prog = saturate(_TankProgress);
                    float filled = (1.0 - smoothstep(prog - _TankSoft, prog + _TankSoft, fc)) * keepTank;
                    float glow = max(_GlowIntensity, 0.0);
                    col.rgb += fillClan * filled * interior * glow;
                    col.a = max(col.a, filled * interior * saturate(0.35 + glow * 0.65) * clan.a);

                    // Bright liquid surface line at the fill level (also kept out of the keycap disc).
                    if (_SurfaceWidth > 0.0005 && prog > 0.001 && prog < 0.999)
                    {
                        float surf = (1.0 - smoothstep(0.0, _SurfaceWidth, abs(fc - prog))) * keepTank;
                        col.rgb += _SurfaceGlow.rgb * surf * interior * _SurfaceGlow.a;
                        col.a = max(col.a, surf * interior * _SurfaceGlow.a);
                    }
                }

                // ── BORDER ring + runner ────────────────────────────────────────
                if (_BorderOn > 0.5)
                {
                    // Perimeter param. Single-owner = ONE runner looping the whole border (0 at top-centre,
                    // clockwise). Shared (dual) = TWO runners splitting from top-centre, each down its own clan
                    // side, meeting at the bottom — abs() makes the param symmetric so both sides light together.
                    float ang;
                    if (_DualClan > 0.5)
                    {
                        float side = abs(atan2(p.x, p.y)) / 3.14159265;   // 0 top-centre → 1 bottom-centre
                        ang = (_BorderInvert > 0.5) ? (1.0 - side) : side;
                    }
                    else
                    {
                        ang = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                        if (_BorderInvert > 0.5) ang = 1.0 - ang;
                    }
                    float bprog = saturate(_BorderProgress);
                    float borderFilled = step(ang, bprog);

                    // Faint unfilled border + bright filled arc.
                    col.rgb = lerp(col.rgb, clan.rgb, rim * _BorderBackAlpha);
                    col.a = max(col.a, rim * _BorderBackAlpha * clan.a);
                    col.rgb += clan.rgb * rim * borderFilled;
                    col.a = max(col.a, rim * borderFilled * clan.a);

                    // Runner (leading tip) — bright head at the fill edge.
                    if (bprog > 0.001 && bprog < 0.999)
                    {
                        float tip = 1.0 - smoothstep(0.0, _RunnerWidth, abs(ang - bprog));
                        col.rgb += _RunnerGlow.rgb * rim * tip * _RunnerGlow.a;
                        col.a = max(col.a, rim * tip * _RunnerGlow.a);
                    }
                }

                // ── SYMBOL (ability/clan glyph) ─────────────────────────────────
                float2 iconUV = (i.uv - 0.5) / max(_IconScale, 0.0001) + 0.5;
                if (iconUV.x >= 0.0 && iconUV.x <= 1.0 && iconUV.y >= 0.0 && iconUV.y <= 1.0)
                {
                    fixed4 sym = tex2D(_IconTex, iconUV);
                    col.rgb = lerp(col.rgb, _IconColor.rgb, sym.a * _IconColor.a * interior);
                    col.a = max(col.a, sym.a * _IconColor.a * interior);
                }

                // ── SHINE sweep (diagonal, at intervals, while available) ───────
                if (_ShineOn > 0.5)
                {
                    float phase = frac(_Time.y / max(_ShineInterval, 0.0001));   // 0..1 each interval
                    // Only the first slice of the interval sweeps; the rest is quiet.
                    float sweep = phase * (_ShineInterval * _ShineSpeed / max(_ShineInterval,0.0001));
                    float band = 1.0 - smoothstep(0.0, _ShineWidth, abs((i.uv.x + i.uv.y) * 0.5 - frac(phase * _ShineSpeed)));
                    float gate = step(phase, 1.0 / max(_ShineSpeed, 1.0));       // active only briefly per interval
                    col.rgb += _ShineColor.rgb * band * gate * interior * _ShineColor.a;
                }

                // ── FLASH (feedback pulse — e.g. Soul Convergence soul gained) ───
                // A whole-interior clan brighten the driver pulses and decays; sells "it filled a step".
                if (_Flash > 0.001)
                {
                    col.rgb += effClan * _Flash * interior;
                    col.a = max(col.a, _Flash * interior * clan.a);
                }

                // ── HOLD implosion + release PULSE (charge abilities — Empower) ──
                // cdist: 0 at centre → ~1 at the corner. Hold = a clan ring contracting to the centre as the
                // charge fills (implosion); Pulse = a clan ring bursting outward from the centre on release.
                float cdist = length(p) / max(length(ext), 0.0001);
                if (_HoldProgress > 0.001)
                {
                    float rr = 1.0 - saturate(_HoldProgress);                     // contracts to centre
                    float ring = 1.0 - smoothstep(0.0, 0.14, abs(cdist - rr));
                    col.rgb += effClan * ring * interior * 0.8;
                    col.a = max(col.a, ring * interior * 0.6 * clan.a);
                }
                if (_Pulse > 0.001)
                {
                    float br = 1.0 - saturate(_Pulse);                            // expands outward
                    float burst = 1.0 - smoothstep(0.0, 0.16, abs(cdist - br));
                    col.rgb += effClan * burst * interior * _Pulse * 1.6;
                    col.a = max(col.a, burst * interior * _Pulse);
                }

                // ── ACTIVE corona (the "solar eclipse" ring while the ability is FIRING) ──
                // A soft, bright clan RING around the centre that PULSES while active; the keycap drives _ActiveGlow
                // per-frame (1 while firing, fading out after). Rings the glyph → an unmistakable "this is firing" cue.
                if (coronaGlow > 0.001)
                {
                    col.rgb += effClan * coronaGlow * 1.9;
                    col.a = max(col.a, coronaGlow);
                }

                // UI plumbing
                col *= i.color;
                col.a *= body;
                col.a = max(col.a, coronaGlow);   // keep the outward corona alive beyond the cell edge (body=0 there)

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
